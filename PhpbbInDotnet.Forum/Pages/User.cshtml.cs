using CryptSharp.Core;
using Dapper;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using PhpbbInDotnet.Database;
using PhpbbInDotnet.Database.Entities;
using PhpbbInDotnet.Forum.Pages.CustomPartials.Email;
using PhpbbInDotnet.Services;
using PhpbbInDotnet.Utilities;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net.Mail;
using System.Threading.Tasks;

namespace PhpbbInDotnet.Forum.Pages
{
    [ValidateAntiForgeryToken]
    public class UserModel : ModelWithLoggedUser
    {
        [BindProperty]
        public PhpbbUsers CurrentUser { get; set; }
        
        [BindProperty]
        public string FirstPassword { get; set; }
        
        [BindProperty, Compare(otherProperty: nameof(FirstPassword), ErrorMessage = "Cele două parole trebuie să fie identice")]
        public string SecondPassword { get; set; }
        
        [BindProperty]
        public IFormFile Avatar { get; set; }
        
        [BindProperty]
        public bool DeleteAvatar { get; set; } = false;
        
        [BindProperty]
        public bool ShowEmail { get; set; } = false;
        
        [BindProperty]
        [Required(ErrorMessage = "Trebuie să introduceți o adresă e e-mail validă.")]
        [EmailAddress(ErrorMessage = "Trebuie să introduceți o adresă e e-mail validă.")]
        public string Email { get; set; }
        
        [BindProperty, ValidateDate(ErrorMessage = "Data introdusă nu este validă"), MaxLength(10, ErrorMessage = "Lungimea maximă este 10 caractere!")]
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

        public int TotalPosts { get; private set; }
        public (int? Id, string Title) PreferredTopic { get; private set; }
        public double PostsPerDay { get; private set; }
        public List<PhpbbUsers> Foes { get; private set; }
        public UserPageMode Mode { get; private set; }

        private readonly StorageService _storageService;
        private readonly WritingToolsService _writingService;

        public UserModel(CommonUtils utils, ForumDbContext context, ForumTreeService forumService, UserService userService, CacheService cacheService, 
            StorageService storageService, WritingToolsService writingService, IConfiguration config, AnonymousSessionCounter sessionCounter)
            : base(context, forumService, userService, cacheService, config, sessionCounter, utils)
        {
            _storageService = storageService;
            _writingService = writingService;
        }

        public async Task<IActionResult> OnGet()
            => await WithRegisteredUser(async (viewingUser) =>
            {
                if ((UserId ?? Constants.ANONYMOUS_USER_ID) == Constants.ANONYMOUS_USER_ID)
                {
                    return RedirectToPage("Error", new { isNotFound = true });
                }
              
                var cur = await _context.PhpbbUsers.AsNoTracking().FirstOrDefaultAsync(u => u.UserId == UserId);
                if (cur == null)
                {
                    return RedirectToPage("Error", new { isNotFound = true });
                }
                await Render(cur);

                ManageFoes = (ManageFoes ?? false) && await CanEdit();
                ViewAsAnother = (ViewAsAnother ?? true) && !ManageFoes.Value;
                
                return Page();
            });

        public async Task<IActionResult> OnPost()
        {
            if (!await CanEdit())
            {
                return RedirectToPage("Error", new { isUnauthorised = true });
            }

            var dbUser = await _context.PhpbbUsers.FirstOrDefaultAsync(u => u.UserId == CurrentUser.UserId);
            if (dbUser == null)
            {
                return RedirectToPage("Error", new { isNotFound = true });
            }

            var isSelf = CurrentUser.UserId == (await GetCurrentUserAsync()).UserId;
            var userMustLogIn = dbUser.UserAllowPm.ToBool() != AllowPM || dbUser.UserDateformat != CurrentUser.UserDateformat;

            if (await IsCurrentUserAdminHere() && dbUser.UsernameClean != _utils.CleanString(CurrentUser.Username) && !string.IsNullOrWhiteSpace(CurrentUser.Username))
            {
                dbUser.Username = CurrentUser.Username;
                dbUser.UsernameClean = _utils.CleanString(CurrentUser.Username);
            }
            dbUser.UserBirthday = Birthday ?? string.Empty;
            dbUser.UserAllowViewemail = ShowEmail.ToByte();
            dbUser.UserAllowPm = AllowPM.ToByte();
            dbUser.UserOcc = CurrentUser.UserOcc ?? string.Empty;
            dbUser.UserFrom = CurrentUser.UserFrom ?? string.Empty;
            dbUser.UserInterests = CurrentUser.UserInterests ?? string.Empty;
            dbUser.UserDateformat = CurrentUser.UserDateformat ?? "dddd, dd.MM.yyyy, HH:mm";
            dbUser.UserSig = string.IsNullOrWhiteSpace(CurrentUser.UserSig) ? string.Empty : await _writingService.PrepareTextForSaving(CurrentUser.UserSig);
            dbUser.UserEditTime = CurrentUser.UserEditTime;
            dbUser.UserWebsite = CurrentUser.UserWebsite ?? string.Empty;
            dbUser.UserRank = UserRank;

            var newColour = CurrentUser.UserColour?.TrimStart('#');
            if (!string.IsNullOrWhiteSpace(newColour) && dbUser.UserColour != newColour)
            {
                dbUser.UserColour = newColour;
                foreach (var f in _context.PhpbbForums.Where(f => f.ForumLastPosterId == dbUser.UserId))
                {
                    f.ForumLastPosterColour = newColour;
                }
                foreach (var t in _context.PhpbbTopics.Where(t => t.TopicLastPosterId == dbUser.UserId))
                {
                    t.TopicLastPosterColour = newColour;
                }
            }

            if (_utils.CalculateCrc32Hash(Email) != dbUser.UserEmailHash && isSelf)
            {
                var registrationCode = Guid.NewGuid().ToString("n");

                dbUser.UserEmail = Email;
                dbUser.UserEmailHash = _utils.CalculateCrc32Hash(Email);
                dbUser.UserInactiveTime = DateTime.UtcNow.ToUnixTimestamp();
                dbUser.UserInactiveReason = UserInactiveReason.ChangedEmailNotConfirmed;
                dbUser.UserActkey = registrationCode;
                dbUser.UserEmailtime = DateTime.UtcNow.ToUnixTimestamp();

                var subject = $"Schimbarea adresei de e-mail de pe \"{_config.GetValue<string>("ForumName")}\"";
                using var emailMessage = new MailMessage
                {
                    From = new MailAddress($"admin@metrouusor.com", _config.GetValue<string>("ForumName")),
                    Subject = subject,
                    Body = await _utils.RenderRazorViewToString(
                        "_WelcomeEmailPartial",
                        new _WelcomeEmailPartialModel
                        {
                            RegistrationCode = registrationCode,
                            Subject = subject,
                            UserName = dbUser.Username
                        },
                        PageContext,
                        HttpContext
                    ),
                    IsBodyHtml = true
                };
                emailMessage.To.Add(Email);
                await _utils.SendEmail(emailMessage);
            }

            if (!string.IsNullOrWhiteSpace(FirstPassword)
                && Crypter.Phpass.Crypt(FirstPassword, dbUser.UserPassword) != dbUser.UserPassword)
            {
                dbUser.UserPassword = Crypter.Phpass.Crypt(FirstPassword, Crypter.Phpass.GenerateSalt());
                dbUser.UserPasschg = DateTime.UtcNow.ToUnixTimestamp();
                userMustLogIn = true;
            }

            if (DeleteAvatar && !string.IsNullOrWhiteSpace(dbUser.UserAvatar))
            {
                if (!_storageService.DeleteAvatar(dbUser.UserId, Path.GetExtension(dbUser.UserAvatar)))
                {
                    ModelState.AddModelError(nameof(Avatar), "Imaginea nu a putut fi ștearsă");
                }

                dbUser.UserAvatarType = 0;
                dbUser.UserAvatarWidth = 0;
                dbUser.UserAvatarHeight = 0;
                dbUser.UserAvatar = null;
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
                        ModelState.AddModelError(nameof(Avatar), "Fișierul trebuie să fie o imagine de dimensiuni maxime 200px x 200px.");
                    }
                    else
                    {
                        if (!string.IsNullOrWhiteSpace(dbUser.UserAvatar))
                        {
                            _storageService.DeleteAvatar(dbUser.UserId, Path.GetExtension(dbUser.UserAvatar));
                        }
                        if (!await _storageService.UploadAvatar(dbUser.UserId, stream, Avatar.FileName))
                        {
                            ModelState.AddModelError(nameof(Avatar), "Imaginea nu a putut fi încărcată");
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
                    _utils.HandleError(ex, $"Failed to upload avatar for {CurrentUser?.UserId ?? dbUser?.UserId ?? 1}");
                    ModelState.AddModelError(nameof(Avatar), "Imaginea nu a putut fi încărcată");
                }
            }

            var userRoles = (await _userService.GetUserRolesLazy()).Select(r => r.RoleId);
            var dbAclRole = await _context.PhpbbAclUsers.FirstOrDefaultAsync(r => r.UserId == dbUser.UserId && userRoles.Contains(r.AuthRoleId));
            if (dbAclRole != null && dbAclRole.AuthRoleId != (AclRole ?? -1))
            {
                _context.PhpbbAclUsers.Remove(dbAclRole);
                if ((AclRole ?? -1) != -1)
                {
                    await _context.PhpbbAclUsers.AddAsync(new PhpbbAclUsers
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

            var dbUserGroup = await _context.PhpbbUserGroup.FirstOrDefaultAsync(g => g.UserId == dbUser.UserId);
            if (GroupId.HasValue && GroupId != dbUserGroup.GroupId)
            {
                var newGroup = new PhpbbUserGroup
                {
                    GroupId = GroupId.Value,
                    GroupLeader = dbUserGroup.GroupLeader,
                    UserId = dbUserGroup.UserId,
                    UserPending = dbUserGroup.UserPending
                };

                _context.PhpbbUserGroup.Remove(dbUserGroup);
                await _context.SaveChangesAsync();

                await _context.PhpbbUserGroup.AddAsync(newGroup);

                var group = await _context.PhpbbGroups.AsNoTracking().FirstOrDefaultAsync(g => g.GroupId == GroupId.Value);
                foreach (var f in _context.PhpbbForums.Where(f => f.ForumLastPosterId == dbUser.UserId))
                {
                    f.ForumLastPosterColour = group.GroupColour;
                }
                foreach (var t in _context.PhpbbTopics.Where(t => t.TopicLastPosterId == dbUser.UserId))
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
                affectedEntries = await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _utils.HandleError(ex, $"Error updating user profile for {CurrentUser?.UserId ?? dbUser?.UserId ?? 1}");
                ModelState.AddModelError(nameof(CurrentUser), "A intervenit o eroare, iar modificările nu au putut fi salvate.");
            }

            if (affectedEntries > 0 && isSelf)
            {
                await ReloadCurrentUser();
                Mode = UserPageMode.Edit;
                return await OnGet();
            }
            else if (affectedEntries > 0 && userMustLogIn)
            {
                var key = $"UserMustLogIn_{dbUser.UsernameClean}";
                await _cacheService.SetInCache(key, true, TimeSpan.FromDays(_config.GetValue<int>("LoginSessionSlidingExpirationDays")));
            }

            await Render(dbUser);
            
            return Page();
        }

        public async Task<IActionResult> OnPostAddFoe()
            => await WithRegisteredUser(async (user) =>
            {
                var cur = await _context.PhpbbUsers.AsNoTracking().FirstOrDefaultAsync(x => x.UserId == UserId);
                var connection = _context.Database.GetDbConnection();
                await connection.OpenIfNeededAsync();
                await connection.ExecuteAsync(
                    "DELETE FROM phpbb_zebra WHERE user_id = @userId AND zebra_id = @otherId;" +
                    "INSERT INTO phpbb_zebra (user_id, zebra_id, friend, foe) VALUES (@userId, @otherId, 0, 1)",
                    new { user.UserId, otherId = cur.UserId }
                );
                Mode = UserPageMode.AddFoe;
                return await OnGet();
            });

        public async Task<IActionResult> OnPostRemoveFoe()
            => await WithRegisteredUser(async (user) =>
            {
                var cur = await _context.PhpbbUsers.AsNoTracking().FirstOrDefaultAsync(x => x.UserId == UserId);
                var connection = _context.Database.GetDbConnection();
                await connection.OpenIfNeededAsync();
                await connection.ExecuteAsync(
                    "DELETE FROM phpbb_zebra WHERE user_id = @userId AND zebra_id = @otherId;",
                    new { user.UserId, otherId = cur.UserId }
                );
                Mode = UserPageMode.RemoveFoe;
                return await OnGet();
            });

        public async Task<IActionResult> OnPostRemoveMultipleFoes()
            => await WithRegisteredUser(async (user) =>
            {
                var cur = await _context.PhpbbUsers.AsNoTracking().FirstOrDefaultAsync(x => x.UserId == UserId);
                using var connection = _context.Database.GetDbConnection();
                await connection.OpenIfNeededAsync();
                await connection.ExecuteAsync(
                    "DELETE FROM phpbb_zebra WHERE user_id = @userId AND zebra_id IN @otherIds;",
                    new { user.UserId, otherIds = SelectedFoes.DefaultIfEmpty() }
                );
                Mode = UserPageMode.RemoveMultipleFoes;
                return await OnGet();
            });

        public async Task<bool> CanEdit() 
            => !(ViewAsAnother ?? false) && ((await GetCurrentUserAsync()).UserId == CurrentUser.UserId || await IsCurrentUserAdminHere());


        private async Task Render(PhpbbUsers cur)
        {
            CurrentUser = cur;
            CurrentUser.UserSig = string.IsNullOrWhiteSpace(CurrentUser.UserSig) ? string.Empty : _writingService.CleanBbTextForDisplay(CurrentUser.UserSig, CurrentUser.UserSigBbcodeUid);
            TotalPosts = await _context.PhpbbPosts.AsNoTracking().CountAsync(p => p.PosterId == cur.UserId);
            var restrictedForums = (await _forumService.GetRestrictedForumList(await GetCurrentUserAsync())).Select(f => f.forumId);
            var preferredTopic = await (
                from p in _context.PhpbbPosts.AsNoTracking()
                where p.PosterId == cur.UserId
                
                join t in _context.PhpbbTopics.AsNoTracking()
                on p.TopicId equals t.TopicId

                where !restrictedForums.Contains(t.ForumId)

                group p by new { t.ForumId, p.TopicId, t.TopicTitle } into groups
                orderby groups.Count() descending
                select groups.Key
            ).FirstOrDefaultAsync();
            string preferredTopicTitle = null;
            if (preferredTopic != null)
            {
                preferredTopicTitle = _forumService.GetPathText((await GetForumTree()).Tree, preferredTopic.ForumId);
                PreferredTopic = (preferredTopic.TopicId, preferredTopicTitle);
            }
            PostsPerDay = TotalPosts / DateTime.UtcNow.Subtract(cur.UserRegdate.ToUtcTime()).TotalDays;
            Email = cur.UserEmail;
            Birthday = cur.UserBirthday;
            AclRole = await _userService.GetUserRole(await _userService.DbUserToLoggedUserAsync(cur));
            var group = await _userService.GetUserGroupAsync(cur.UserId);
            GroupId = group?.GroupId;
            UserRank = cur.UserRank == 0 ? group.GroupRank : cur.UserRank;
            AllowPM = cur.UserAllowPm.ToBool();
            ShowEmail = cur.UserAllowViewemail.ToBool();
            Foes = await (
                from z in _context.PhpbbZebra.AsNoTracking()
                where z.UserId == cur.UserId && z.Foe == 1

                join u in _context.PhpbbUsers.AsNoTracking()
                on z.ZebraId equals u.UserId
                into joined

                from j in joined
                select j
            ).ToListAsync();
        }
    }
}