using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Serverless.Forum.ForumDb;
using Serverless.Forum.Utilities;
using CryptSharp.Core;
using Microsoft.AspNetCore.Http;
using Amazon.S3.Model;
using System.Net;
using System.IO;
using Amazon.S3;
using Amazon;
using System.Net.Mail;

namespace Serverless.Forum.Pages
{
    public class UserModel : ModelWithLoggedUser
    {
        public bool IsSelf => CurrentUser.UserId == CurrentUserId;
        public async Task<bool> CanEditAsync() => IsSelf || await IsCurrentUserAdminHereAsync();
        public int TotalPosts { get; private set; }
        public (int? Id, string Title) PreferredTopic { get; private set; }
        public double PostsPerDay { get; private set; }

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

        private readonly IAmazonS3 _s3Client;

        public UserModel(IConfiguration config, Utils utils) : base(config, utils)
        {
            _s3Client = new AmazonS3Client(_config["AwsS3Key"], _config["AwsS3Secret"], RegionEndpoint.EUCentral1);
        }

        public async Task<IActionResult> OnGet(int? UserId)
        {
            if ((UserId ?? 1) == 1)
            {
                return Forbid();
            }

            using (var context = new ForumDbContext(_config))
            {
                CurrentUser = await context.PhpbbUsers.FirstOrDefaultAsync(u => u.UserId == UserId);
                if (CurrentUser == null)
                {
                    return NotFound($"Utilizatorul cu id '{UserId}' nu există.");
                }

                TotalPosts = await context.PhpbbPosts.CountAsync(p => p.PosterId == UserId);
                var preferredTopicId = await (
                    from p in context.PhpbbPosts
                    where p.PosterId == UserId
                    group p by p.TopicId into groups
                    orderby groups.Count() descending
                    select groups.Key as int?
                ).FirstOrDefaultAsync();
                var preferredTopicTitle = (await context.PhpbbTopics.FirstOrDefaultAsync(t => t.TopicId == preferredTopicId))?.TopicTitle;
                PreferredTopic = (preferredTopicId, preferredTopicTitle);
                PostsPerDay = TotalPosts / DateTime.UtcNow.Subtract(CurrentUser.UserRegdate.ToUtcTime()).TotalDays;
                Email = CurrentUser.UserEmail;
                Birthday = CurrentUser.UserBirthday;
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

                CurrentUser.UserBirthday = Birthday;
                CurrentUser.UserAllowViewemail = (byte)(ShowEmail ? 1 : 0);

                if (Email != CurrentUser.UserEmail)
                {
                    var registrationCode = Guid.NewGuid().ToString("n");

                    CurrentUser.UserEmail = Email;
                    CurrentUser.UserInactiveTime = DateTime.UtcNow.ToUnixTimestamp();
                    CurrentUser.UserInactiveReason = UserInactiveReason.ChangedEmailNotConfirmed;
                    CurrentUser.UserActkey = registrationCode;
                    CurrentUser.UserEmailtime = DateTime.UtcNow.ToUnixTimestamp();

                    var subject = $"Schimbarea adresei de e-mail de pe \"{Constants.FORUM_NAME}\"";
                    var emailMessage = new MailMessage
                    {
                        From = new MailAddress($"admin@metrouusor.com", Constants.FORUM_NAME),
                        Subject = subject,
                        Body =
                            $"<h2>{subject}</h2><br/><br/>" +
                            "Pentru a continua, trebuie să îți confirmi adresa de email.<br/><br/>" +
                            $"<a href=\"{Constants.FORUM_BASE_URL}/Confirm?code={registrationCode}&username={CurrentUser.UsernameClean}&handler=ConfirmEmail\">Apasă aici</a> pentru a o confirma.<br/><br/>" +
                            "O zi bună!",
                        IsBodyHtml = true
                    };
                    emailMessage.To.Add(Email);
                    await _utils.SendEmail(emailMessage);
                }

                if (!string.IsNullOrWhiteSpace(FirstPassword)
                    && Crypter.Phpass.Crypt(FirstPassword, CurrentUser.UserPassword) != CurrentUser.UserPassword)
                {
                    CurrentUser.UserPassword = Crypter.Phpass.Crypt(FirstPassword, Crypter.Phpass.GenerateSalt());
                    CurrentUser.UserPasschg = DateTime.UtcNow.ToUnixTimestamp();
                }

                if (DeleteAvatar)
                {
                    var request = new DeleteObjectRequest
                    {
                        BucketName = _config["AwsS3BucketName"],
                        Key = $"avatars/{CurrentUserId}{Path.GetExtension(dbUser.UserAvatar)}"
                    };

                    var response = await _s3Client.DeleteObjectAsync(request);

                    if (response.HttpStatusCode != HttpStatusCode.OK)
                    {
                        throw new Exception("Failed to delete file");
                    }

                    CurrentUser.UserAvatarType = 0;
                    CurrentUser.UserAvatarWidth = 0;
                    CurrentUser.UserAvatarHeight = 0;
                    CurrentUser.UserAvatar = null;
                }

                if (Avatar != null)
                {
                    var request = new PutObjectRequest
                    {
                        BucketName = _config["AwsS3BucketName"],
                        Key = $"avatars/{CurrentUserId}{Path.GetExtension(Avatar.FileName)}",
                        ContentType = Avatar.ContentType,
                        InputStream = Avatar.OpenReadStream()
                    };

                    var response = await _s3Client.PutObjectAsync(request);
                    if (response.HttpStatusCode != HttpStatusCode.OK)
                    {
                        throw new Exception("Failed to upload file");
                    }

                    var bmp = Avatar.ToImage();
                    CurrentUser.UserAvatarType = 1;
                    CurrentUser.UserAvatarWidth = unchecked((short)bmp.Width);
                    CurrentUser.UserAvatarHeight = unchecked((short)bmp.Height);
                    CurrentUser.UserAvatar = $"{CurrentUser.UserId}_{Avatar.FileName}";
                }

                dbUser = CurrentUser;

                await context.SaveChangesAsync();

                return Page();
            }
        }
    }
}