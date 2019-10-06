using Amazon;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using Serverless.Forum.forum;
using Serverless.Forum.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;

namespace Serverless.Forum.Pages
{
    public class PostingModel : ModelWithLoggedUser
    {
        public forumContext DbContext => _dbContext;
        private readonly IAmazonS3 _s3Client;
        private readonly IConfiguration _config;

        public Lazy<List<PhpbbBbcodes>> DbBbCodes => 
            new Lazy<List<PhpbbBbcodes>>(() =>
                (from c in _dbContext.PhpbbBbcodes
                 where c.DisplayOnPosting == 1
                 select c).ToList()
        );

        public Lazy<List<string>> BbCodes =>
            new Lazy<List<string>>(() =>
            {
                var codes = new List<string>(Constants.BBCODES);
                foreach (var bbCode in DbBbCodes.Value)
                {
                    codes.Add($"[{bbCode.BbcodeTag}]");
                    codes.Add($"[/{bbCode.BbcodeTag}]");
                }
                return codes;
            }
        );

        public Lazy<Dictionary<string, string>> BbCodeHelplines =>
            new Lazy<Dictionary<string, string>>(() =>
            {
                var helplines = new Dictionary<string, string>(Constants.BBCODE_HELPLINES);
                foreach (var bbCode in DbBbCodes.Value)
                {
                    var index = BbCodes.Value.IndexOf($"[{bbCode.BbcodeTag}]");
                    helplines.Add($"cb_{index}", bbCode.BbcodeHelpline);
                }
                return helplines;
            }
        );

        public (string Codes, string HelpLines) BbCodesForJs => 
            (JsonConvert.SerializeObject(BbCodes.Value, Formatting.Indented), JsonConvert.SerializeObject(BbCodeHelplines.Value, Formatting.Indented));

        ////https://stackoverflow.com/questions/54963951/aws-lambda-file-upload-to-asp-net-core-2-1-razor-page-is-corrupting-binary
        //[BindProperty]
        //[Required]
        //[Display(Name = "Attachment")]
        //public IFormFile Attachment { get; set; }

        public string TopicTitle { get; set; }
        public string PostTitle { get; set; }
        public string PostText { get; set; }
        public int ForumId { get; set; }
        public int TopicId { get; set; }
        public Lazy<List<PhpbbAttachments>> PostAttachments => new Lazy<List<PhpbbAttachments>>(() => new List<PhpbbAttachments>());

        public PostingModel(forumContext context, IConfiguration config) : base(context)
        {
            _config = config;
            _s3Client = new AmazonS3Client(_config["AwsS3Key"], _config["AwsS3Secret"], RegionEndpoint.EUCentral1);
        }

        public async Task<IActionResult> OnGetForumPost(int forumId, int topicId)
        {
            if ((await GetCurrentUserAsync()).UserId == 1)
            {
                return Unauthorized();
            }

            ForumId = forumId;
            TopicId = topicId;
            var curTopic = await (from t in _dbContext.PhpbbTopics
                                  where t.TopicId == topicId
                                  select t)
                            .FirstOrDefaultAsync();

            if (curTopic == null)
            {
                return NotFound();
            }

            TopicTitle = curTopic.TopicTitle;
            PostTitle = $"{Constants.REPLY}{TopicTitle}";

            return Page();
        }

        public async Task<IActionResult> OnGetQuoteForumPost(int postId)
        {
            if ((await GetCurrentUserAsync()).UserId == 1)
            {
                return Unauthorized();
            }

            var curPost = await (from p in _dbContext.PhpbbPosts
                                 where p.PostId == postId
                                 select p).FirstOrDefaultAsync();

            if (curPost == null)
            {
                return NotFound();
            }

            ForumId = curPost.ForumId;
            TopicId = curPost.TopicId;

            var curTopic = await (from t in _dbContext.PhpbbTopics
                                  where t.TopicId == TopicId
                                  select t).FirstOrDefaultAsync();

            if (curTopic == null)
            {
                return NotFound();
            }

            TopicTitle = curTopic.TopicTitle;

            var subject = curPost.PostSubject.StartsWith(Constants.REPLY) ? curPost.PostSubject.Substring(Constants.REPLY.Length) : curPost.PostSubject;
            PostTitle = PostTitle = $"{Constants.REPLY}{subject}";

            var curAuthor = curPost.PostUsername;
            if (string.IsNullOrWhiteSpace(curAuthor))
            {
                curAuthor = await (from u in _dbContext.PhpbbUsers
                                   where u.UserId == curPost.PosterId
                                   select u.Username).FirstOrDefaultAsync();
            }

            PostText = $"[quote=\"{curAuthor}\"]{Environment.NewLine}{HttpUtility.HtmlDecode(RemoveBbCodeUid(curPost.PostText, curPost.BbcodeUid))}{Environment.NewLine}[/quote]";

            return Page();
        }

        public async Task<IActionResult> OnPostAttachment(IList<IFormFile> files, string fileComment)
        {
            var usr = await GetCurrentUserAsync();
            if (usr.UserId == 1)
            {
                return RedirectToPage("Login", new LoginModel(_dbContext));
            }

            foreach (var file in files)
            {
                var name = $"{usr.UserId ?? 0}_{Guid.NewGuid():n}";
                var request = new PutObjectRequest
                {
                    BucketName = _config["AwsS3BucketName"],
                    Key = name,
                    ContentType = file.ContentType,
                    InputStream = file.OpenReadStream()
                };

                var response = await _s3Client.PutObjectAsync(request);
                if (response.HttpStatusCode != HttpStatusCode.OK)
                {
                    throw new Exception("Failed to upload file");
                }

                PostAttachments.Value.Add(new PhpbbAttachments
                {
                    AttachComment = fileComment,
                    Extension = System.IO.Path.GetExtension(file.FileName),
                    Filetime = DateTime.UtcNow.LocalTimeToTimestamp(),
                    Filesize = file.Length,
                    Mimetype = file.ContentType,
                    PhysicalFilename = name,
                    RealFilename = System.IO.Path.GetFileName(file.FileName),
                    PosterId = usr.UserId.Value,
                    TopicId = TopicId
                });
            }
            return Page();
        }

        public async Task<IActionResult> OnPostForumPost(int forumId, int topicId, string postSubject, string postText, string returnUrl)
        {
            var usr = await GetCurrentUserAsync();
            if (usr.UserId == 1)
            {
                return Unauthorized();
            }


            foreach (var sr in from s in _dbContext.PhpbbSmilies
                               select new
                               {
                                   Regex = new Regex(Regex.Escape(s.Code), RegexOptions.Compiled | RegexOptions.Singleline),
                                   Replacement = $"<img src=\"./images/smilies/{s.SmileyUrl.Trim('/')}\" />"
                               })
            {
                postText = sr.Regex.Replace(postText, sr.Replacement);
            }
            todo: add smiley pane in html
            await _dbContext.PhpbbPosts.AddAsync(new PhpbbPosts
            {
                ForumId = forumId,
                TopicId = topicId,
                PosterId = usr.UserId.Value,
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
                PostAttachment = (byte)(PostAttachments.Value.Any() ? 1 : 0),
                PostChecksum = CalculateMD5Hash(HttpUtility.HtmlEncode(postText)),
                PostEditCount = 0,
                PostEditLocked = 0,
                PostEditReason = string.Empty,
                PostEditTime = 0,
                PostEditUser = 0,
                PosterIp = HttpContext.Connection.RemoteIpAddress.ToString()
            });

            await _dbContext.SaveChangesAsync();

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

        private string RemoveBbCodeUid(string text, string uid)
        {
            var uidRegex = new Regex($":{uid}", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            var tagRegex = new Regex(@"(:[a-z])(\]|:)", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            var cleanTextTemp = uidRegex.Replace(text, string.Empty);
            return tagRegex.Replace(cleanTextTemp, "$2");
        }
    }
}