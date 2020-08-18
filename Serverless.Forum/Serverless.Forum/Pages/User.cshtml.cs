using CryptSharp.Core;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Serverless.Forum.ForumDb;
using Serverless.Forum.ForumDb.Entities;
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
    [ValidateAntiForgeryToken, ResponseCache(Location = ResponseCacheLocation.None, NoStore = true, Duration = 0)]
    public class UserModel : ModelWithLoggedUser
    {
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

        public async Task<bool> CanEditAsync() => !ViewAsAnother && ((await GetCurrentUserAsync()).UserId == CurrentUser.UserId || await IsCurrentUserAdminHereAsync());
        public int TotalPosts { get; private set; }
        public (int? Id, string Title) PreferredTopic { get; private set; }
        public double PostsPerDay { get; private set; }
        public bool ViewAsAnother { get; set; }

        private readonly Utils _utils;
        private readonly StorageService _storageService;
        private readonly WritingToolsService _writingService;
        private readonly IConfiguration _config;

        public UserModel(Utils utils, ForumDbContext context, ForumTreeService forumService, UserService userService, CacheService cacheService, 
            StorageService storageService, WritingToolsService writingService, IConfiguration config)
            : base(context, forumService, userService, cacheService)
        {
            _utils = utils;
            _storageService = storageService;
            _writingService = writingService;
            _config = config;
        }

        public async Task<IActionResult> OnGet(int? userId, bool? viewAsAnother)
            => await WithRegisteredUser(async () =>
            {
                if ((userId ?? Constants.ANONYMOUS_USER_ID) == Constants.ANONYMOUS_USER_ID)
                {
                    return BadRequest("Nu pot fi schimbate detaliile utilizatorului anonim!");
                }

                ViewAsAnother = viewAsAnother ?? false;

                var cur = await _context.PhpbbUsers.AsNoTracking().FirstOrDefaultAsync(u => u.UserId == userId);
                if (cur == null)
                {
                    return RedirectToPage("Error", new { isNotFound = true });
                }
                await Render(_context, cur);

                return Page();
            });

        public async Task<IActionResult> OnPost()
        {
            if (!await CanEditAsync())
            {
                return RedirectToPage("Login", new { returnUrl = @HttpUtility.UrlEncode(HttpContext.Request.Path + HttpContext.Request.QueryString) });
            }

            var dbUser = await _context.PhpbbUsers.FirstOrDefaultAsync(u => u.UserId == CurrentUser.UserId);
            if (dbUser == null)
            {
                return RedirectToPage("Error", new { isNotFound = true });
            }

            var userMustLogIn = false;

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

            if (DeleteAvatar)
            {
                if (!_storageService.DeleteAvatar(dbUser.UserId))
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
                var name = await _storageService.UploadAvatar(dbUser.UserId, Avatar);
                if (string.IsNullOrWhiteSpace(name))
                {
                    throw new Exception("Failed to upload file");
                }

                using var bmp = Avatar.ToImage();
                dbUser.UserAvatarType = 1;
                dbUser.UserAvatarWidth = unchecked((short)bmp.Width);
                dbUser.UserAvatarHeight = unchecked((short)bmp.Height);
                dbUser.UserAvatar = name;
            }

            var userRoles = (await _userService.GetUserRolesLazy()).Select(r => r.RoleId);
            var dbAclRole = await _context.PhpbbAclUsers.FirstOrDefaultAsync(r => r.UserId == dbUser.UserId && userRoles.Contains(r.AuthRoleId));
            if (dbAclRole != null && AclRole == -1)
            {
                _context.PhpbbAclUsers.Remove(dbAclRole);
                userMustLogIn = true;
            }
            else if (dbAclRole != null && AclRole.HasValue && AclRole.Value != dbAclRole.AuthRoleId)
            {
                dbAclRole.AuthRoleId = AclRole.Value;
                userMustLogIn = true;
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
                userMustLogIn = true;
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

            if (userMustLogIn)
            {
                var key = $"UserMustLogIn_{dbUser.UsernameClean}";
                await _cacheService.SetInCache(key, true);
            }

            return Page();
        }

        private async Task Render(ForumDbContext context, PhpbbUsers cur)
        {
            CurrentUser = cur;
            CurrentUser.UserSig = string.IsNullOrWhiteSpace(CurrentUser.UserSig) ? string.Empty : _writingService.CleanBbTextForDisplay(CurrentUser.UserSig, CurrentUser.UserSigBbcodeUid);
            TotalPosts = await context.PhpbbPosts.AsNoTracking().CountAsync(p => p.PosterId == cur.UserId);
            var restrictedForums = (await _forumService.GetRestrictedForumList(await GetCurrentUserAsync())).Select(f => f.forumId);
            var preferredTopic = await (
                from p in context.PhpbbPosts.AsNoTracking()
                where p.PosterId == cur.UserId
                
                join t in context.PhpbbTopics.AsNoTracking()
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
            GroupId = await _userService.GetUserGroupAsync(cur.UserId);
            UserRank = cur.UserRank;
        }
    }
}