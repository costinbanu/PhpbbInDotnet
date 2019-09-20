﻿using Amazon;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Configuration;
using Serverless.Forum.Contracts;
using Serverless.Forum.forum;
using Serverless.Forum.Utilities;
using System;
using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Web;

namespace Serverless.Forum.Pages
{
    public class PostingModel : PageModel
    {
        private const string REPLY = "Re: ";

        private readonly forumContext _dbContext;
        private readonly IAmazonS3 _s3Client;
        private readonly IConfiguration _config;
        private bool hasAttachments = false;

        public LoggedUser CurrentUser
        {
            get
            {
                var user = User;
                if (!user.Identity.IsAuthenticated)
                {
                    user = Acl.Instance.GetAnonymousUser(_dbContext);
                    Task.WaitAll(
                        HttpContext.SignInAsync(
                            CookieAuthenticationDefaults.AuthenticationScheme, 
                            user, 
                            new AuthenticationProperties
                            {
                                AllowRefresh = true,
                                ExpiresUtc = DateTimeOffset.Now.AddMonths(1),
                                IsPersistent = true,
                            }
                        )
                    );
                }
                return user.ToLoggedUser();
            }
        }

        //https://stackoverflow.com/questions/54963951/aws-lambda-file-upload-to-asp-net-core-2-1-razor-page-is-corrupting-binary
        [BindProperty]
        [Required]
        [Display(Name = "Attachment")]
        public IFormFile Attachment { get; set; }

        public string TopicTitle { get; set; }
        public string PostTitle { get; set; }
        public string PostText { get; set; }
        public int ForumId { get; set; }
        public int TopicId { get; set; }

        public PostingModel(forumContext context, IConfiguration config)
        {
            _dbContext = context;
            _config = config;
            _s3Client = new AmazonS3Client(_config["AwsS3Key"], _config["AwsS3Secret"], RegionEndpoint.EUCentral1);
        }

        public IActionResult OnGetForumPost(int forumId, int topicId)
        {
            ForumId = forumId;
            TopicId = topicId;
            var curTopic = (from t in _dbContext.PhpbbTopics
                            where t.TopicId == topicId
                            select t).FirstOrDefault();

            if (curTopic == null)
            {
                return NotFound();
            }

            TopicTitle = curTopic.TopicTitle;
            PostTitle = $"{REPLY}{TopicTitle}";

            return Page();
        }

        public IActionResult OnGetQuoteForumPost(int postId)
        {
            var curPost = (from p in _dbContext.PhpbbPosts
                           where p.PostId == postId
                           select p).FirstOrDefault();

            if (curPost == null)
            {
                return NotFound();
            }

            ForumId = curPost.ForumId;
            TopicId = curPost.TopicId;

            var curTopic = (from t in _dbContext.PhpbbTopics
                            where t.TopicId == TopicId
                            select t).FirstOrDefault();

            if (curTopic == null)
            {
                return NotFound();
            }

            TopicTitle = curTopic.TopicTitle;

            var subject = curPost.PostSubject.StartsWith(REPLY) ? curPost.PostSubject.Substring(REPLY.Length) : curPost.PostSubject;
            PostTitle = PostTitle = $"{REPLY}{subject}";

            var curAuthor = curPost.PostUsername;
            if (string.IsNullOrWhiteSpace(curAuthor))
            {
                curAuthor = (from u in _dbContext.PhpbbUsers
                              where u.UserId == curPost.PosterId
                              select u.Username).FirstOrDefault();
            }

            PostText = $"[quote=\"{curAuthor}\"]{Environment.NewLine}{HttpUtility.HtmlDecode(curPost.PostText)}{Environment.NewLine}[/quote]";

            return Page();
        }

        public async Task<IActionResult> OnPostAttachment()
        {
            if (CurrentUser.UserId == 1)
            {
                return Unauthorized();
            }

            var request = new PutObjectRequest
            {
                BucketName = _config["AwsS3BucketName"],
                Key = $"{CurrentUser.UserId ?? 0}_{Guid.NewGuid():n}".ToLowerInvariant(),
                ContentType = Attachment.ContentType,
                InputStream = Attachment.OpenReadStream()
            };

            var response = await _s3Client.PutObjectAsync(request);
            if (response.HttpStatusCode != HttpStatusCode.OK)
            {
                throw new Exception("Failed to upload file");
            }

            hasAttachments = true;

            return Page();
        }

        public async Task<IActionResult> OnPostForumPost(int forumId, int topicId, string postSubject, string postText, string returnUrl)
        {
            if (CurrentUser.UserId == 1)
            {
                return Unauthorized();
            }

            await _dbContext.PhpbbPosts.AddAsync(new PhpbbPosts
            {
                ForumId = forumId,
                TopicId = topicId,
                PosterId = CurrentUser.UserId.Value,
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
            throw await Task.FromResult(new NotImplementedException());
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
            var hash = MD5.Create().ComputeHash(Encoding.UTF8.GetBytes(input));
            var sb = new StringBuilder();
            for (int i = 0; i < hash.Length; i++)
            {
                sb.Append(hash[i].ToString("x2"));
            }
            return sb.ToString();
        }
    }
}