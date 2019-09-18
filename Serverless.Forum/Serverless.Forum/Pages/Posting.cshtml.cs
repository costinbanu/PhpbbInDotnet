using Amazon;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Configuration;
using Serverless.Forum.forum;
using Serverless.Forum.Utilities;
using System;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Web;

namespace Serverless.Forum.Pages
{
    public class PostingModel : PageModel
    {
        forumContext _dbContext;
        IAmazonS3 _s3Client;
        IConfiguration _config;
        bool hasAttachments = false;

        [BindProperty]
        //[Required]
        //[Display(Name = "Workbook")]
        public IFormFile Attachment { get; set; }

        public PostingModel(forumContext context, IConfiguration config)
        {
            _dbContext = context;
            _config = config;
            _s3Client = new AmazonS3Client(_config["AwsS3Key"], _config["AwsS3Secret"], RegionEndpoint.EUCentral1);

        }

        public async Task OnPostAttachment()
        {
            var user = User;
            if (!user.Identity.IsAuthenticated)
            {
                user = Acl.Instance.GetAnonymousUser(_dbContext);
                await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, user, new AuthenticationProperties
                {
                    AllowRefresh = true,
                    ExpiresUtc = DateTimeOffset.Now.AddMonths(1),
                    IsPersistent = true,
                });
            }

            var request = new PutObjectRequest
            {
                BucketName = _config["AwsS3BucketName"],
                Key = $"{user.ToLoggedUser().UserId?.ToString()}_{Guid.NewGuid():n}".ToLowerInvariant(),
                ContentType = Attachment.ContentType,
                InputStream = Attachment.OpenReadStream()
            };

            var response = await _s3Client.PutObjectAsync(request);
            if (response.HttpStatusCode != HttpStatusCode.OK)
            {
                throw new Exception("Failed to upload file");
            }

            hasAttachments = true;
        }

        public async Task<IActionResult> OnPostForumPost(int forumId, int topicId, string postSubject, string postText, string returnUrl)
        {
            var user = User;
            if (!user.Identity.IsAuthenticated)
            {
                user = Acl.Instance.GetAnonymousUser(_dbContext);
                await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, user, new AuthenticationProperties
                {
                    AllowRefresh = true,
                    ExpiresUtc = DateTimeOffset.Now.AddMonths(1),
                    IsPersistent = true,
                });
            }

            var loggedUser = user.ToLoggedUser();

            if (loggedUser.UserId == 1)
            {
                return Unauthorized();
            }

            await _dbContext.PhpbbPosts.AddAsync(new PhpbbPosts
            {
                ForumId = forumId,
                TopicId = topicId,
                PosterId = loggedUser.UserId.Value,
                PostSubject = HttpUtility.HtmlEncode(postSubject),
                PostText = HttpUtility.HtmlEncode(postText),
                PostTime = DateTime.UtcNow.LocalTimeToTimestamp(),
                PostApproved = 1,
                PostReported = 0,
                BbcodeUid = RandomString(),
                EnableBbcode = 1,
                EnableMagicUrl = 1,
                EnableSig = 1,
                EnableSmilies = 1,
                PostAttachment = (byte)(hasAttachments ? 1 : 0),
                PostChecksum = CalculateMD5Hash(HttpUtility.HtmlEncode(postText)),
                PostEditCount = 0,
                PostEditLocked = 0,
                PostEditReason = string.Empty,
                PostEditTime = 0,
                PostEditUser = 0,
                PosterIp = HttpContext.Connection.RemoteIpAddress.ToString()
            }).ConfigureAwait(false);

            await _dbContext.SaveChangesAsync().ConfigureAwait(false);

            return RedirectToPage(HttpUtility.UrlDecode(returnUrl));
        }

        public async Task<IActionResult> OnPostPrivateMessage()
        {

        }

        private string RandomString(int length = 8)
        {
            var chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
            var stringChars = new char[8];
            var random = new Random();

            for (int i = 0; i < stringChars.Length; i++)
            {
                stringChars[i] = chars[random.Next(chars.Length)];
            }

            return new string(stringChars);
        }

        private string CalculateMD5Hash(string input)
        {
            var md5 = MD5.Create();
            byte[] inputBytes = Encoding.UTF8.GetBytes(input);
            byte[] hash = md5.ComputeHash(inputBytes);

            var sb = new StringBuilder();
            for (int i = 0; i < hash.Length; i++)
            {
                sb.Append(hash[i].ToString("x2"));
            }
            return sb.ToString();
        }
    }
}