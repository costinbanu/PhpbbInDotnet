using CryptSharp.Core;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.EntityFrameworkCore;
using Serverless.Forum.ForumDb;
using Serverless.Forum.Pages.CustomPartials.Email;
using Serverless.Forum.Services;
using Serverless.Forum.Utilities;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Net.Mail;
using System.Threading.Tasks;
using System.Web;

namespace Serverless.Forum.Pages
{
    public class UserModel : ModelWithLoggedUser
    {
        public bool IsSelf => CurrentUser.UserId == CurrentUserId;
        public async Task<bool> CanEditAsync() => !ViewAsAnother && (IsSelf || await IsCurrentUserAdminHereAsync());
        public int TotalPosts { get; private set; }
        public (int? Id, string Title) PreferredTopic { get; private set; }
        public double PostsPerDay { get; private set; }
        public bool ViewAsAnother { get; set; }

        [BindProperty]
        public PhpbbUsers CurrentUser { get; set; }
        [BindProperty]
        public string FirstPassword { get; set; }
        [BindProperty, Compare(otherProperty: nameof(FirstPassword), ErrorMessage = "Cele două parole trebuie să fie identice")]
        public string SecondPassword { get; set; }
        [BindProperty, ValidateFile(ErrorMessage = "Imaginea este coruptă sau prea mare!")]
        public IFormFile Avatar { get; set; }
        [BindProperty]
        public bool DeleteAvatar { get; set; } = false;
        [BindProperty]
        public bool ShowEmail { get; set; } = true;
        [BindProperty]
        [Required(ErrorMessage = "Trebuie să introduceți o adresă e e-mail validă.")]
        [EmailAddress(ErrorMessage = "Trebuie să introduceți o adresă e e-mail validă.")]
        public string Email { get; set; }
        [BindProperty, ValidateDate(ErrorMessage = "Data introdusă nu este validă")]
        public string Birthday { get; set; }
        [BindProperty]
        public int? AclRole { get; set; }
        [BindProperty]
        public int? GroupId { get; set; }
        [BindProperty]
        public int UserRank { get; set; }

        private readonly Utils _utils;
        private readonly StorageService _storageService;
        private readonly WritingToolsService _writingService;

        public UserModel(Utils utils, ForumDbContext context, ForumTreeService forumService, UserService userService, CacheService cacheService, StorageService storageService, WritingToolsService writingService)
            : base(context, forumService, userService, cacheService)
        {
            _utils = utils;
            _storageService = storageService;
            _writingService = writingService;
        }

        public async Task<IActionResult> OnGet(int? userId, bool? viewAsAnother)
        {
            if ((userId ?? Constants.ANONYMOUS_USER_ID) == Constants.ANONYMOUS_USER_ID)
            {
                return Forbid();
            }

            var response = await PageAuthorizationResponses().FirstOrDefaultAsync();
            if (response != null)
            {
                return response;
            }

            ViewAsAnother = viewAsAnother ?? false;

            var cur = await _context.PhpbbUsers.AsNoTracking().FirstOrDefaultAsync(u => u.UserId == userId);
            if (cur == null)
            {
                return NotFound($"Utilizatorul cu id '{userId}' nu există.");
            }
            await Render(_context, cur);

            return Page();
        }

        public async Task<IActionResult> OnPost()
        {
            if (!await CanEditAsync())
            {
                return Forbid();
            }

            var dbUser = await _context.PhpbbUsers.FirstOrDefaultAsync(u => u.UserId == CurrentUser.UserId);
            if (dbUser == null)
            {
                return NotFound($"Utilizatorul cu id '{CurrentUser.UserId}' nu există.");
            }

            dbUser.UserBirthday = Birthday ?? string.Empty;
            dbUser.UserAllowViewemail = (byte)(ShowEmail ? 1 : 0);
            dbUser.UserRank = UserRank;
            dbUser.UserOcc = CurrentUser.UserOcc ?? string.Empty;
            dbUser.UserInterests = CurrentUser.UserInterests ?? string.Empty;
            dbUser.UserDateformat = CurrentUser.UserDateformat ?? "dddd, dd.MM.yyyy, HH:mm";
            dbUser.UserSig = string.IsNullOrWhiteSpace(CurrentUser.UserSig) ? string.Empty : _writingService.PrepareTextForSaving(HttpUtility.HtmlEncode(CurrentUser.UserSig));
            dbUser.UserEditTime = CurrentUser.UserEditTime;
            dbUser.UserWebsite = CurrentUser.UserWebsite ?? string.Empty;

            if (_utils.CalculateCrc32Hash(Email) != dbUser.UserEmailHash)
            {
                var registrationCode = Guid.NewGuid().ToString("n");

                dbUser.UserEmail = Email;
                dbUser.UserEmailHash = _utils.CalculateCrc32Hash(Email);
                dbUser.UserInactiveTime = DateTime.UtcNow.ToUnixTimestamp();
                dbUser.UserInactiveReason = UserInactiveReason.ChangedEmailNotConfirmed;
                dbUser.UserActkey = registrationCode;
                dbUser.UserEmailtime = DateTime.UtcNow.ToUnixTimestamp();

                var subject = $"Schimbarea adresei de e-mail de pe \"{Constants.FORUM_NAME}\"";
                using var emailMessage = new MailMessage
                {
                    From = new MailAddress($"admin@metrouusor.com", Constants.FORUM_NAME),
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
                var key = $"UserMustLogIn_{dbUser.UsernameClean}";
                await _cacheService.SetInCache(key, true);
            }

            if (DeleteAvatar)
            {
                if (!await _storageService.DeleteFile($"{_storageService.FolderPrefix}{_storageService.AvatarsFolder}{dbUser.UserId}{Path.GetExtension(dbUser.UserAvatar)}"))
                {
                    throw new Exception("Failed to delete file");
                }

                dbUser.UserAvatarType = 0;
                dbUser.UserAvatarWidth = 0;
                dbUser.UserAvatarHeight = 0;
                dbUser.UserAvatar = null;
            }

            if (Avatar != null)
            {
                if (!await _storageService.UploadFile($"{_storageService.FolderPrefix}{_storageService.AvatarsFolder}{dbUser.UserId}{Path.GetExtension(Avatar.FileName)}", Avatar.ContentType, Avatar.OpenReadStream()))
                {
                    throw new Exception("Failed to upload file");
                }

                using var bmp = Avatar.ToImage();
                dbUser.UserAvatarType = 1;
                dbUser.UserAvatarWidth = unchecked((short)bmp.Width);
                dbUser.UserAvatarHeight = unchecked((short)bmp.Height);
                dbUser.UserAvatar = $"{dbUser.UserId}_{Avatar.FileName}";
            }

            var dbAclRole = await _context.PhpbbAclUsers.FirstOrDefaultAsync(r => r.UserId == dbUser.UserId);
            if (dbAclRole != null && AclRole == -1)
            {
                _context.PhpbbAclUsers.Remove(dbAclRole);
            }
            else if (dbAclRole != null && AclRole.HasValue && AclRole.Value != dbAclRole.AuthRoleId)
            {
                dbAclRole.AuthRoleId = AclRole.Value;
            }
            else if (dbAclRole == null && AclRole.HasValue && AclRole.Value != -1)
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
            }

            await _context.SaveChangesAsync();

            await Render(_context, dbUser);

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                await _userService.DbUserToClaimsPrincipalAsync(dbUser),
                new AuthenticationProperties
                {
                    AllowRefresh = true,
                    ExpiresUtc = DateTimeOffset.Now.AddMonths(1),
                    IsPersistent = true,
                }
            );
            return Page();
        }

        private async Task Render(ForumDbContext context, PhpbbUsers cur)
        {
            CurrentUser = cur;
            CurrentUser.UserSig = string.IsNullOrWhiteSpace(CurrentUser.UserSig) ? string.Empty : HttpUtility.HtmlDecode(_writingService.CleanBbTextForDisplay(CurrentUser.UserSig, CurrentUser.UserSigBbcodeUid));
            TotalPosts = await context.PhpbbPosts.AsNoTracking().CountAsync(p => p.PosterId == cur.UserId);
            var restrictedForums = (await GetCurrentUserAsync())?.AllPermissions?.Where(p => p.AuthRoleId == 16)?.Select(p => p.ForumId) ?? Enumerable.Empty<int>();
            var preferredTopicId = await (
                from p in context.PhpbbPosts.AsNoTracking()
                where p.PosterId == cur.UserId
                
                join t in context.PhpbbTopics.AsNoTracking()
                on p.TopicId equals t.TopicId
                
                join f in restrictedForums
                on t.ForumId equals f
                into joinedForums
                
                from jf in joinedForums.DefaultIfEmpty()
                where jf == default
                group p by p.TopicId into groups
                orderby groups.Count() descending
                select groups.Key as int?
            ).FirstOrDefaultAsync();
            var preferredTopicTitle = (await context.PhpbbTopics.FirstOrDefaultAsync(t => t.TopicId == preferredTopicId))?.TopicTitle;
            if (preferredTopicId.HasValue)
            {
                var pathParts = new List<string>(_forumService.GetPathInTree(await GetForumTreeAsync(), f => f.Name, -1, preferredTopicId.Value).Skip(1))
                {
                    preferredTopicTitle
                };
                preferredTopicTitle = string.Join(" → ", pathParts);
            }
            PreferredTopic = (preferredTopicId, preferredTopicTitle);
            PostsPerDay = TotalPosts / DateTime.UtcNow.Subtract(cur.UserRegdate.ToUtcTime()).TotalDays;
            Email = cur.UserEmail;
            Birthday = cur.UserBirthday;
            AclRole = await _userService.GetUserRole(await _userService.DbUserToLoggedUserAsync(cur));
            GroupId = await _userService.GetUserGroupAsync(cur.UserId);
            UserRank = cur.UserRank;
        }
    }
}