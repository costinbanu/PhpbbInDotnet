using Amazon;
using Amazon.S3;
using Amazon.S3.Model;
using CryptSharp.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Serverless.Forum.ForumDb;
using Serverless.Forum.Pages.CustomPartials.Email;
using Serverless.Forum.Services;
using Serverless.Forum.Utilities;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;

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

        private readonly IAmazonS3 _s3Client;

        public UserModel(IConfiguration config, Utils utils, ForumTreeService forumService, UserService userService, CacheService cacheService)
            : base(config, utils, forumService, userService, cacheService)
        {
            _s3Client = new AmazonS3Client(_config["AwsS3Key"], _config["AwsS3Secret"], RegionEndpoint.EUCentral1);
        }

        public async Task<IActionResult> OnGet(int? userId, bool? viewAsAnother)
        {
            if ((userId ?? 1) == 1)
            {
                return Forbid();
            }

            var response = await ValidatePagePermissionsResponsesAsync();
            if (response != null)
            {
                return response;
            }

            ViewAsAnother = viewAsAnother ?? false;

            using (var context = new ForumDbContext(_config))
            {
                var cur = await context.PhpbbUsers.AsNoTracking().FirstOrDefaultAsync(u => u.UserId == userId);
                if (cur == null)
                {
                    return NotFound($"Utilizatorul cu id '{userId}' nu există.");
                }
                await Render(context, cur);
            }

            return Page();
        }

        public async Task<IActionResult> OnPost()
        {
            if (!await CanEditAsync())
            {
                return Forbid();
            }

            using (var context = new ForumDbContext(_config))
            {
                var dbUser = await context.PhpbbUsers.FirstOrDefaultAsync(u => u.UserId == CurrentUser.UserId);
                if (dbUser == null)
                {
                    return NotFound($"Utilizatorul cu id '{CurrentUser.UserId}' nu există.");
                }

                dbUser.UserBirthday = Birthday ?? string.Empty;
                dbUser.UserAllowViewemail = (byte)(ShowEmail ? 1 : 0);
                dbUser.UserRank = UserRank;

                if (Email != dbUser.UserEmail)
                {
                    var registrationCode = Guid.NewGuid().ToString("n");

                    dbUser.UserEmail = Email;
                    dbUser.UserInactiveTime = DateTime.UtcNow.ToUnixTimestamp();
                    dbUser.UserInactiveReason = UserInactiveReason.ChangedEmailNotConfirmed;
                    dbUser.UserActkey = registrationCode;
                    dbUser.UserEmailtime = DateTime.UtcNow.ToUnixTimestamp();

                    var subject = $"Schimbarea adresei de e-mail de pe \"{Constants.FORUM_NAME}\"";
                    using (var emailMessage = new MailMessage
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
                    })
                    {
                        emailMessage.To.Add(Email);
                        await _utils.SendEmail(emailMessage);
                    }
                }

                if (!string.IsNullOrWhiteSpace(FirstPassword)
                    && Crypter.Phpass.Crypt(FirstPassword, dbUser.UserPassword) != dbUser.UserPassword)
                {
                    dbUser.UserPassword = Crypter.Phpass.Crypt(FirstPassword, Crypter.Phpass.GenerateSalt());
                    dbUser.UserPasschg = DateTime.UtcNow.ToUnixTimestamp();
                }

                if (DeleteAvatar)
                {
                    var request = new DeleteObjectRequest
                    {
                        BucketName = _config["AwsS3BucketName"],
                        Key = $"avatars/{dbUser.UserId}{Path.GetExtension(dbUser.UserAvatar)}"
                    };

                    var response = await _s3Client.DeleteObjectAsync(request);

                    if (response.HttpStatusCode != HttpStatusCode.OK)
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
                    var request = new PutObjectRequest
                    {
                        BucketName = _config["AwsS3BucketName"],
                        Key = $"avatars/{dbUser.UserId}{Path.GetExtension(Avatar.FileName)}",
                        ContentType = Avatar.ContentType,
                        InputStream = Avatar.OpenReadStream()
                    };

                    var response = await _s3Client.PutObjectAsync(request);
                    if (response.HttpStatusCode != HttpStatusCode.OK)
                    {
                        throw new Exception("Failed to upload file");
                    }

                    using (var bmp = Avatar.ToImage())
                    {
                        dbUser.UserAvatarType = 1;
                        dbUser.UserAvatarWidth = unchecked((short)bmp.Width);
                        dbUser.UserAvatarHeight = unchecked((short)bmp.Height);
                        dbUser.UserAvatar = $"{dbUser.UserId}_{Avatar.FileName}";
                    }
                }

                var dbAclRole = await context.PhpbbAclUsers.FirstOrDefaultAsync(r => r.UserId == dbUser.UserId);
                if (dbAclRole != null && AclRole == -1)
                {
                    context.PhpbbAclUsers.Remove(dbAclRole);
                }
                else if (dbAclRole != null && AclRole.HasValue && AclRole.Value != dbAclRole.AuthRoleId)
                {
                    dbAclRole.AuthRoleId = AclRole.Value;
                }
                else if (dbAclRole == null && AclRole.HasValue && AclRole.Value != -1)
                {
                    await context.PhpbbAclUsers.AddAsync(new PhpbbAclUsers
                    {
                        AuthOptionId = 0,
                        AuthRoleId = AclRole.Value,
                        AuthSetting = 0,
                        ForumId = 0,
                        UserId = dbUser.UserId
                    });
                }

                var dbUserGroup = await context.PhpbbUserGroup.FirstOrDefaultAsync(g => g.UserId == dbUser.UserId);
                if (GroupId.HasValue && GroupId != dbUserGroup.GroupId)
                {
                    var newGroup = new PhpbbUserGroup
                    {
                        GroupId = GroupId.Value,
                        GroupLeader = dbUserGroup.GroupLeader,
                        UserId = dbUserGroup.UserId,
                        UserPending = dbUserGroup.UserPending
                    };

                    context.PhpbbUserGroup.Remove(dbUserGroup);
                    await context.SaveChangesAsync();

                    await context.PhpbbUserGroup.AddAsync(newGroup);

                    var group = await context.PhpbbGroups.AsNoTracking().FirstOrDefaultAsync(g => g.GroupId == GroupId.Value);
                    foreach (var f in context.PhpbbForums.Where(f => f.ForumLastPosterId == dbUser.UserId))
                    {
                        f.ForumLastPosterColour = group.GroupColour;
                    }
                    foreach (var t in context.PhpbbTopics.Where(t => t.TopicLastPosterId == dbUser.UserId))
                    {
                        t.TopicLastPosterColour = group.GroupColour;
                    }
                    dbUser.UserColour = group.GroupColour;
                }

                await context.SaveChangesAsync();

                await Render(context, dbUser);

                return Page();
            }
        }

        private async Task Render(ForumDbContext context, PhpbbUsers cur)
        {
            CurrentUser = cur;
            TotalPosts = await context.PhpbbPosts.AsNoTracking().CountAsync(p => p.PosterId == cur.UserId);
            var preferredTopicId = await (
                from p in context.PhpbbPosts
                where p.PosterId == cur.UserId
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
            GroupId = await _userService.GeUserGroupAsync(cur.UserId);
            UserRank = cur.UserRank;
        }
    }
}