﻿using CryptSharp.Core;
using Dapper;
using LazyCache;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using PhpbbInDotnet.Database;
using PhpbbInDotnet.Database.Entities;
using PhpbbInDotnet.Languages;
using PhpbbInDotnet.Objects;
using PhpbbInDotnet.Services;
using PhpbbInDotnet.Utilities;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Mail;
using System.Threading.Tasks;

namespace PhpbbInDotnet.Forum.Pages
{
    [ValidateAntiForgeryToken]
    public class UserModel : AuthenticatedPageModel
    {
        [BindProperty]
        public PhpbbUsers CurrentUser { get; set; }
        
        [BindProperty]
        public string FirstPassword { get; set; }
        
        [BindProperty]
        public string SecondPassword { get; set; }
        
        [BindProperty]
        public IFormFile Avatar { get; set; }
        
        [BindProperty]
        public bool DeleteAvatar { get; set; } = false;
        
        [BindProperty]
        public bool ShowEmail { get; set; } = false;
        
        [BindProperty]
        public string Email { get; set; }
        
        [BindProperty]
        public string Birthday { get; set; }
        
        [BindProperty]
        public int? AclRole { get; set; }
        
        [BindProperty]
        public int? GroupId { get; set; }

        [BindProperty(SupportsGet = true)]
        public int? UserId { get; set; }

        [BindProperty]
        public int UserRank { get; set; }

        [BindProperty(SupportsGet = true)]
        public bool? ViewAsAnother { get; set; }

        [BindProperty]
        public bool AllowPM { get; set; }

        [BindProperty(SupportsGet = true)]
        public bool? ManageFoes { get; set; }

        [BindProperty]
        public int SelectedFoeId { get; set; }

        [BindProperty]
        public int[] SelectedFoes { get; set; }

        [BindProperty]
        public bool JumpToUnread { get; set; }

        public int TotalPosts { get; private set; }
        public (int? Id, string Title) PreferredTopic { get; private set; }
        public double PostsPerDay { get; private set; }
        public List<PhpbbUsers> Foes { get; private set; }
        public long AttachCount { get; private set; }
        public long AttachTotalSize { get; private set; }
        public UserPageMode Mode { get; private set; }
        public bool EmailChanged { get; private set; }
        public new IConfiguration Config => base.Config;

        private readonly StorageService _storageService;
        private readonly WritingToolsService _writingService;
        private readonly OperationLogService _operationLogService;

        public UserModel(CommonUtils utils, ForumDbContext context, ForumTreeService forumService, UserService userService, IAppCache cache, StorageService storageService, 
            WritingToolsService writingService, IConfiguration config, AnonymousSessionCounter sessionCounter, LanguageProvider languageProvider, OperationLogService operationLogService)
            : base(context, forumService, userService, cache, config, sessionCounter, utils, languageProvider)
        {
            _storageService = storageService;
            _writingService = writingService;
            _operationLogService = operationLogService;
        }

        public async Task<IActionResult> OnGet()
            => await WithRegisteredUser(async (viewingUser) =>
            {
                if ((UserId ?? Constants.ANONYMOUS_USER_ID) == Constants.ANONYMOUS_USER_ID)
                {
                    return RedirectToPage("Error", new { isNotFound = true });
                }
              
                var cur = await Context.PhpbbUsers.AsNoTracking().FirstOrDefaultAsync(u => u.UserId == UserId);
                if (cur == null)
                {
                    return RedirectToPage("Error", new { isNotFound = true });
                }
                await Render(cur);

                ManageFoes = (ManageFoes ?? false) && await CanEdit();
                ViewAsAnother = (ViewAsAnother ?? true) && !ManageFoes.Value;
                JumpToUnread = cur.JumpToUnread.ToBool();
                
                return Page();
            });

        public async Task<IActionResult> OnPost()
        {
            if (!await CanEdit())
            {
                return RedirectToPage("Error", new { isUnauthorised = true });
            }

            var dbUser = await Context.PhpbbUsers.FirstOrDefaultAsync(u => u.UserId == CurrentUser.UserId);
            if (dbUser == null)
            {
                return RedirectToPage("Error", new { isNotFound = true });
            }

            var currentUserId = (await GetCurrentUserAsync()).UserId;
            var isSelf = CurrentUser.UserId == currentUserId;
            var userMustLogIn = dbUser.UserAllowPm.ToBool() != AllowPM || dbUser.UserDateformat != CurrentUser.UserDateformat;
            var lang = await GetLanguage();
            var validator = new UserProfileDataValidationService(ModelState, LanguageProvider, lang);

            var newCleanUsername = Utils.CleanString(CurrentUser.Username);
            if (await IsCurrentUserAdminHere() && dbUser.UsernameClean != newCleanUsername && !string.IsNullOrWhiteSpace(CurrentUser.Username))
            {
                if (!validator.ValidateUsername(nameof(CurrentUser), CurrentUser.Username))
                {
                    return Page();
                }

                if (await Context.PhpbbUsers.AsNoTracking().AnyAsync(u => u.UsernameClean == newCleanUsername))
                {
                    ModelState.AddModelError(nameof(CurrentUser), LanguageProvider.Errors[lang, "EXISTING_USERNAME"]);
                    return Page();
                }

                dbUser.Username = CurrentUser.Username;
                dbUser.UsernameClean = newCleanUsername;
                foreach (var f in Context.PhpbbForums.Where(f => f.ForumLastPosterId == dbUser.UserId))
                {
                    f.ForumLastPosterName = CurrentUser.Username;
                }
                foreach (var t in Context.PhpbbTopics.Where(t => t.TopicLastPosterId == dbUser.UserId))
                {
                    t.TopicLastPosterName = CurrentUser.Username;
                }
                userMustLogIn = true;
            }

            if (!string.IsNullOrWhiteSpace(Birthday) && Birthday != dbUser.UserBirthday)
            {
                if (!DateTime.TryParse(Birthday, out _))
                {
                    ModelState.AddModelError(nameof(Birthday), LanguageProvider.Errors[lang, "INVALID_DATE"]);
                    return Page();
                }
            }

            dbUser.UserBirthday = Birthday ?? string.Empty;
            dbUser.UserAllowViewemail = ShowEmail.ToByte();
            dbUser.UserAllowPm = AllowPM.ToByte();
            dbUser.UserOcc = CurrentUser.UserOcc ?? string.Empty;
            dbUser.UserFrom = CurrentUser.UserFrom ?? string.Empty;
            dbUser.UserInterests = CurrentUser.UserInterests ?? string.Empty;
            dbUser.UserDateformat = CurrentUser.UserDateformat ?? LanguageProvider.GetDefaultDateFormat(lang);
            dbUser.UserSig = string.IsNullOrWhiteSpace(CurrentUser.UserSig) ? string.Empty : await _writingService.PrepareTextForSaving(CurrentUser.UserSig);
            dbUser.UserEditTime = CurrentUser.UserEditTime;
            dbUser.UserWebsite = CurrentUser.UserWebsite ?? string.Empty;
            if (UserRank > 0)
            {
                dbUser.UserRank = UserRank;
            }
            dbUser.UserStyle = CurrentUser.UserStyle;
            dbUser.JumpToUnread = JumpToUnread.ToByte();
            dbUser.UserLang = CurrentUser.UserLang;

            var newColour = CurrentUser.UserColour?.TrimStart('#');
            if (!string.IsNullOrWhiteSpace(newColour) && dbUser.UserColour != newColour)
            {
                dbUser.UserColour = newColour;
                foreach (var f in Context.PhpbbForums.Where(f => f.ForumLastPosterId == dbUser.UserId))
                {
                    f.ForumLastPosterColour = newColour;
                }
                foreach (var t in Context.PhpbbTopics.Where(t => t.TopicLastPosterId == dbUser.UserId))
                {
                    t.TopicLastPosterColour = newColour;
                }
            }

            var newEmailHash = Utils.CalculateCrc32Hash(Email);
            if (newEmailHash != dbUser.UserEmailHash)
            {
                if (!validator.ValidateEmail(nameof(Email), Email))
                {
                    return Page();
                }

                if (await Context.PhpbbUsers.AsNoTracking().AnyAsync(u => u.UserEmailHash == newEmailHash))
                {
                    ModelState.AddModelError(nameof(Email), LanguageProvider.Errors[lang, "EXISTING_EMAIL"]);
                    return Page();
                }


                dbUser.UserEmail = Email;
                dbUser.UserEmailHash = newEmailHash;
                dbUser.UserEmailtime = DateTime.UtcNow.ToUnixTimestamp();

                if (isSelf)
                {
                    var registrationCode = Guid.NewGuid().ToString("n");
                    dbUser.UserInactiveTime = DateTime.UtcNow.ToUnixTimestamp();
                    dbUser.UserInactiveReason = UserInactiveReason.ChangedEmailNotConfirmed;
                    dbUser.UserActkey = registrationCode;

                    var subject = string.Format(LanguageProvider.Email[lang, "EMAIL_CHANGED_SUBJECT_FORMAT"], Config.GetValue<string>("ForumName"));
                    using var emailMessage = new MailMessage
                    {
                        From = new MailAddress($"admin@metrouusor.com", Config.GetValue<string>("ForumName")),
                        Subject = subject,
                        Body = await Utils.RenderRazorViewToString(
                            "_WelcomeEmailPartial",
                            new WelcomeEmailDto
                            {
                                RegistrationCode = registrationCode,
                                Subject = subject,
                                UserName = dbUser.Username,
                                Language = dbUser.UserLang
                            },
                            PageContext,
                            HttpContext
                        ),
                        IsBodyHtml = true
                    };
                    emailMessage.To.Add(Email);
                    await Utils.SendEmail(emailMessage);
                }

                userMustLogIn = true;
                EmailChanged = true;
            }

            var passwordChanged = false;
            if (!string.IsNullOrWhiteSpace(FirstPassword) && Crypter.Phpass.Crypt(FirstPassword, dbUser.UserPassword) != dbUser.UserPassword)
            {
                var validations = new[]
                {
                    validator.ValidatePassword(nameof(FirstPassword), FirstPassword),
                    validator.ValidateSecondPassword(nameof(SecondPassword), SecondPassword, FirstPassword)
                };

                if (!validations.All(x => x))
                {
                    return Page();
                }

                dbUser.UserPassword = Crypter.Phpass.Crypt(FirstPassword, Crypter.Phpass.GenerateSalt());
                dbUser.UserPasschg = DateTime.UtcNow.ToUnixTimestamp();
                userMustLogIn = true;
                passwordChanged = true;
            }

            if (DeleteAvatar && !string.IsNullOrWhiteSpace(dbUser.UserAvatar))
            {
                if (!_storageService.DeleteAvatar(dbUser.UserId, Path.GetExtension(dbUser.UserAvatar)))
                {
                    ModelState.AddModelError(nameof(Avatar), LanguageProvider.Errors[lang, "DELETE_AVATAR_ERROR"]);
                    return Page();
                }

                dbUser.UserAvatarType = 0;
                dbUser.UserAvatarWidth = 0;
                dbUser.UserAvatarHeight = 0;
                dbUser.UserAvatar = string.Empty;
            }

            if (Avatar != null)
            {
                try
                {
                    using var stream = Avatar.OpenReadStream();
                    using var bmp = new Bitmap(stream);
                    stream.Seek(0, SeekOrigin.Begin);
                    if (bmp.Width >200 || bmp.Height > 200)
                    {
                        ModelState.AddModelError(nameof(Avatar), LanguageProvider.Errors[lang, "AVATAR_FORMAT_ERROR"]);
                        return Page();
                    }
                    else
                    {
                        if (!string.IsNullOrWhiteSpace(dbUser.UserAvatar))
                        {
                            _storageService.DeleteAvatar(dbUser.UserId, Path.GetExtension(dbUser.UserAvatar));
                        }
                        if (!await _storageService.UploadAvatar(dbUser.UserId, stream, Avatar.FileName))
                        {
                            ModelState.AddModelError(nameof(Avatar), LanguageProvider.Errors[lang, "AVATAR_UPLOAD_ERROR"]);
                            return Page();
                        }
                        else
                        {
                            dbUser.UserAvatarType = 1;
                            dbUser.UserAvatarWidth = unchecked((short)bmp.Width);
                            dbUser.UserAvatarHeight = unchecked((short)bmp.Height);
                            dbUser.UserAvatar = Avatar.FileName;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Utils.HandleError(ex, $"Failed to upload avatar for {CurrentUser?.UserId ?? dbUser?.UserId ?? 1}");
                    ModelState.AddModelError(nameof(Avatar), LanguageProvider.Errors[lang, "AVATAR_UPLOAD_ERROR"]);
                    return Page();
                }
            }

            var userRoles = (await UserService.GetUserRolesLazy()).Select(r => r.RoleId);
            var dbAclRole = await Context.PhpbbAclUsers.FirstOrDefaultAsync(r => r.UserId == dbUser.UserId && userRoles.Contains(r.AuthRoleId));
            if (dbAclRole != null && dbAclRole.AuthRoleId != (AclRole ?? -1))
            {
                Context.PhpbbAclUsers.Remove(dbAclRole);
                if ((AclRole ?? -1) != -1)
                {
                    await Context.PhpbbAclUsers.AddAsync(new PhpbbAclUsers
                    {
                        AuthOptionId = 0,
                        AuthRoleId = AclRole.Value,
                        AuthSetting = 0,
                        ForumId = 0,
                        UserId = dbUser.UserId
                    });
                }
                userMustLogIn = true;
            }

            var dbUserGroup = await Context.PhpbbUserGroup.FirstOrDefaultAsync(g => g.UserId == dbUser.UserId);
            if (dbUserGroup == null)
            {
                if (dbUser.GroupId == 0)
                {
                    throw new InvalidOperationException($"User {dbUser.UserId} has no group associated neither in phpbb_users, nor in phpbb_user_group.");
                }
                await Context.PhpbbUserGroup.AddAsync(new PhpbbUserGroup
                {
                    GroupId = dbUser.GroupId,
                    UserId = dbUser.UserId
                });
                await Context.SaveChangesAsync();
            }
            else if (GroupId.HasValue && GroupId != dbUserGroup.GroupId)
            {
                var newGroup = new PhpbbUserGroup
                {
                    GroupId = GroupId.Value,
                    GroupLeader = dbUserGroup.GroupLeader,
                    UserId = dbUserGroup.UserId,
                    UserPending = dbUserGroup.UserPending
                };

                Context.PhpbbUserGroup.Remove(dbUserGroup);
                await Context.SaveChangesAsync();

                await Context.PhpbbUserGroup.AddAsync(newGroup);

                var group = await Context.PhpbbGroups.AsNoTracking().FirstOrDefaultAsync(g => g.GroupId == GroupId.Value);
                foreach (var f in Context.PhpbbForums.Where(f => f.ForumLastPosterId == dbUser.UserId))
                {
                    f.ForumLastPosterColour = group.GroupColour;
                }
                foreach (var t in Context.PhpbbTopics.Where(t => t.TopicLastPosterId == dbUser.UserId))
                {
                    t.TopicLastPosterColour = group.GroupColour;
                }
                dbUser.UserColour = group.GroupColour;
                dbUser.GroupId = group.GroupId;
                userMustLogIn = true;
            }

            var affectedEntries = 0;
            try
            {
                affectedEntries = await Context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                Utils.HandleError(ex, $"Error updating user profile for {CurrentUser?.UserId ?? dbUser?.UserId ?? 1}");
                ModelState.AddModelError(nameof(CurrentUser), LanguageProvider.Errors[lang, "AN_ERROR_OCCURRED_TRY_AGAIN"]);
                return Page();
            }

            if (affectedEntries > 0 && isSelf && !EmailChanged)
            {
                await ReloadCurrentUser();
                Mode = UserPageMode.Edit;
                return await OnGet();
            }
            else if (affectedEntries > 0 && userMustLogIn)
            {
                var key = $"UserMustLogIn_{dbUser.UsernameClean}";
                Cache.Add(key, true, TimeSpan.FromDays(Config.GetValue<int>("LoginSessionSlidingExpirationDays")));
                if (EmailChanged)
                {
                    Mode = UserPageMode.Edit;
                }
            }

            if (EmailChanged)
            {
                await _operationLogService.LogUserProfileAction(UserProfileActions.ChangeEmail, currentUserId, dbUser);
            }
            if (passwordChanged)
            {
                await _operationLogService.LogUserProfileAction(UserProfileActions.ChangePassword, currentUserId, dbUser);
            }

            await Render(dbUser);
            
            return Page();
        }

        public async Task<IActionResult> OnPostAddFoe()
            => await WithRegisteredUser(async (user) =>
            {
                var cur = await Context.PhpbbUsers.AsNoTracking().FirstOrDefaultAsync(x => x.UserId == UserId);
                var connection = Context.Database.GetDbConnection();
                await connection.ExecuteAsync(
                    "DELETE FROM phpbb_zebra WHERE user_id = @userId AND zebra_id = @otherId;" +
                    "INSERT INTO phpbb_zebra (user_id, zebra_id, friend, foe) VALUES (@userId, @otherId, 0, 1)",
                    new { user.UserId, otherId = cur.UserId }
                );
                await ReloadCurrentUser();
                Mode = UserPageMode.AddFoe;
                return await OnGet();
            });

        public async Task<IActionResult> OnPostRemoveFoe()
            => await WithRegisteredUser(async (user) =>
            {
                var cur = await Context.PhpbbUsers.AsNoTracking().FirstOrDefaultAsync(x => x.UserId == UserId);
                var connection = Context.Database.GetDbConnection();
                await connection.ExecuteAsync(
                    "DELETE FROM phpbb_zebra WHERE user_id = @userId AND zebra_id = @otherId;",
                    new { user.UserId, otherId = cur.UserId }
                );
                await ReloadCurrentUser();
                Mode = UserPageMode.RemoveFoe;
                return await OnGet();
            });

        public async Task<IActionResult> OnPostRemoveMultipleFoes()
            => await WithRegisteredUser(async (user) =>
            {
                var cur = await Context.PhpbbUsers.AsNoTracking().FirstOrDefaultAsync(x => x.UserId == UserId);
                using var connection = Context.Database.GetDbConnection();
                await connection.ExecuteAsync(
                    "DELETE FROM phpbb_zebra WHERE user_id = @userId AND zebra_id IN @otherIds;",
                    new { user.UserId, otherIds = SelectedFoes.DefaultIfEmpty() }
                );
                await ReloadCurrentUser();
                Mode = UserPageMode.RemoveMultipleFoes;
                return await OnGet();
            });

        public async Task<bool> CanEdit() 
            => !(ViewAsAnother ?? false) && ((await GetCurrentUserAsync()).UserId == CurrentUser.UserId || await IsCurrentUserAdminHere());

        private async Task Render(PhpbbUsers cur)
        {
            CurrentUser = cur;
            CurrentUser.UserSig = string.IsNullOrWhiteSpace(CurrentUser.UserSig) ? string.Empty : _writingService.CleanBbTextForDisplay(CurrentUser.UserSig, CurrentUser.UserSigBbcodeUid);
            TotalPosts = cur.UserPosts;
            var restrictedForums = (await ForumService.GetRestrictedForumList(await GetCurrentUserAsync())).Select(f => f.forumId);
            var preferredTopic = await (
                from p in Context.PhpbbPosts.AsNoTracking()
                where p.PosterId == cur.UserId

                join t in Context.PhpbbTopics.AsNoTracking()
                on p.TopicId equals t.TopicId

                where !restrictedForums.Contains(t.ForumId)

                group p by new { t.ForumId, p.TopicId, t.TopicTitle } into groups
                orderby groups.Count() descending
                select groups.Key
            ).FirstOrDefaultAsync();
            string preferredTopicTitle = null;
            if (preferredTopic != null)
            {
                preferredTopicTitle = ForumService.GetPathText((await GetForumTree(false, false)).Tree, preferredTopic.ForumId);
                PreferredTopic = (preferredTopic.TopicId, preferredTopicTitle);
            }
            PostsPerDay = TotalPosts / DateTime.UtcNow.Subtract(cur.UserRegdate.ToUtcTime()).TotalDays;
            Email = cur.UserEmail;
            Birthday = cur.UserBirthday;
            AclRole = await UserService.GetUserRole(await UserService.DbUserToAuthenticatedUser(cur));
            var group = await UserService.GetUserGroup(cur.UserId);
            GroupId = group?.GroupId;
            UserRank = cur.UserRank == 0 ? group.GroupRank : cur.UserRank;
            AllowPM = cur.UserAllowPm.ToBool();
            ShowEmail = cur.UserAllowViewemail.ToBool();
            Foes = await (
                from z in Context.PhpbbZebra.AsNoTracking()
                where z.UserId == cur.UserId && z.Foe == 1

                join u in Context.PhpbbUsers.AsNoTracking()
                on z.ZebraId equals u.UserId
                into joined

                from j in joined
                select j
            ).ToListAsync();
            var result = await Context.Database.GetDbConnection().QueryFirstOrDefaultAsync(
                "SELECT sum(a.filesize) as size, count(a.attach_id) as cnt " +
                "FROM phpbb_attachments a " +
                "JOIN phpbb_posts p ON a.post_msg_id = p.post_id " +
                "WHERE p.poster_id = @userId",
                new { cur.UserId }
            );
            AttachCount = (long?)result?.cnt ?? 0L;
            AttachTotalSize = (long?)result?.size ?? 0L;
        }
    }
}