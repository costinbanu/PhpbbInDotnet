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
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;

namespace Serverless.Forum.Pages
{
    [ValidateAntiForgeryToken]
    public class PostingModel : ModelWithLoggedUser
    {
        private readonly IAmazonS3 _s3Client;

        public List<PhpbbBbcodes> DbBbCodes { get; private set; }
        public List<string> BbCodes { get; private set; }
        public Dictionary<string, string> BbCodeHelplines { get; private set; }
        public (string Codes, string HelpLines) BbCodesForJs => (
            JsonConvert.SerializeObject(BbCodes), 
            JsonConvert.SerializeObject(BbCodeHelplines)
        );
        public List<PhpbbSmilies> Smilies { get; private set; }
        public List<KeyValuePair<string, string>> Users { get; private set; }

        ////https://stackoverflow.com/questions/54963951/aws-lambda-file-upload-to-asp-net-core-2-1-razor-page-is-corrupting-binary
        //[BindProperty]
        //[Required]
        //[Display(Name = "Attachment")]
        //public IFormFile Attachment { get; set; }

        public string TopicTitle { get; private set; }
        public string PostTitle { get; private set; }
        public string PostText { get; private set; }
        public int ForumId { get; private set; }
        public int TopicId { get; private set; }
        public List<PhpbbAttachments> PostAttachments;

        public PostingModel(IConfiguration config, Utils utils) : base(config, utils)
        {
            _s3Client = new AmazonS3Client(_config["AwsS3Key"], _config["AwsS3Secret"], RegionEndpoint.EUCentral1);
            PostAttachments = new List<PhpbbAttachments>();
        }

        public async Task<IActionResult> OnGetForumPost(int forumId, int topicId)
        {
            if (CurrentUserId == 1)
            {
                return Unauthorized();
            }

            ForumId = forumId;
            TopicId = topicId;
            PhpbbTopics curTopic = null;
            using (var context = new forumContext(_config))
            {
                curTopic = await (from t in context.PhpbbTopics
                                  where t.TopicId == topicId
                                  select t).FirstOrDefaultAsync();

                await Init(context);
            }

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
            if (CurrentUserId == 1)
            {
                return Unauthorized();
            }

            PhpbbPosts curPost = null;
            PhpbbTopics curTopic = null;
            string curAuthor = null;

            using (var context = new forumContext(_config))
            {
                curPost = await (from p in context.PhpbbPosts
                                     where p.PostId == postId
                                     select p).FirstOrDefaultAsync();

                if (curPost == null)
                {
                    return NotFound();
                }

                curTopic = await (from t in context.PhpbbTopics
                                      where t.TopicId == curPost.TopicId
                                      select t).FirstOrDefaultAsync();

                if (curTopic == null)
                {
                    return NotFound();
                }

                curAuthor = curPost.PostUsername;
                if (string.IsNullOrWhiteSpace(curAuthor))
                {
                    curAuthor = await (from u in context.PhpbbUsers
                                       where u.UserId == curPost.PosterId
                                       select u.Username).FirstOrDefaultAsync();
                }

                await Init(context);
            }


            ForumId = curPost.ForumId;
            TopicId = curPost.TopicId;
            TopicTitle = curTopic.TopicTitle;

            var subject = curPost.PostSubject.StartsWith(Constants.REPLY) ? curPost.PostSubject.Substring(Constants.REPLY.Length) : curPost.PostSubject;
            PostTitle = PostTitle = $"{Constants.REPLY}{subject}";
            PostText = $"[quote=\"{curAuthor}\"]\n{HttpUtility.HtmlDecode(RemoveBbCodeUid(curPost.PostText, curPost.BbcodeUid))}\n[/quote]";

            return Page();
        }

        public async Task<IActionResult> OnPostAttachment(IList<IFormFile> files, string fileComment)
        {
            if (CurrentUserId == 1)
            {
                return RedirectToPage("Login");
            }

            foreach (var file in files)
            {
                var name = $"{CurrentUserId ?? 0}_{Guid.NewGuid():n}";
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

                PostAttachments.Add(new PhpbbAttachments
                {
                    AttachComment = fileComment,
                    Extension = System.IO.Path.GetExtension(file.FileName),
                    Filetime = DateTime.UtcNow.LocalTimeToTimestamp(),
                    Filesize = file.Length,
                    Mimetype = file.ContentType,
                    PhysicalFilename = name,
                    RealFilename = System.IO.Path.GetFileName(file.FileName),
                    //PosterId = usr.UserId.Value,
                    //TopicId = TopicId
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

            using (var context = new forumContext(_config))
            {
                foreach (var sr in from s in context.PhpbbSmilies
                                   select new
                                   {
                                       Regex = new Regex(Regex.Escape(s.Code), RegexOptions.Compiled | RegexOptions.Singleline),
                                       Replacement = $"<img src=\"./images/smilies/{s.SmileyUrl.Trim('/')}\" />"
                                   })
                {
                    postText = sr.Regex.Replace(postText, sr.Replacement);
                }

                var result = await context.PhpbbPosts.AddAsync(new PhpbbPosts
                {
                    ForumId = forumId,
                    TopicId = topicId,
                    PosterId = usr.UserId.Value,
                    PostSubject = HttpUtility.HtmlEncode(postSubject),
                    PostText = HttpUtility.HtmlEncode(postText),
                    PostTime = DateTime.UtcNow.LocalTimeToTimestamp(),
                    PostApproved = 1,
                    PostReported = 0,
                    BbcodeUid = _utils.RandomString(),
                    EnableBbcode = 1,
                    EnableMagicUrl = 1,
                    EnableSig = 1,
                    EnableSmilies = 1,
                    PostAttachment = (byte)(PostAttachments.Any() ? 1 : 0),
                    PostChecksum = _utils.CalculateMD5Hash(HttpUtility.HtmlEncode(postText)),
                    PostEditCount = 0,
                    PostEditLocked = 0,
                    PostEditReason = string.Empty,
                    PostEditTime = 0,
                    PostEditUser = 0,
                    PosterIp = HttpContext.Connection.RemoteIpAddress.ToString()
                });

                PostAttachments.ForEach(a => a.PostMsgId = result.Entity.PostId);
                await context.PhpbbAttachments.AddRangeAsync(PostAttachments);

                await context.SaveChangesAsync();
            }

            return RedirectToPage(HttpUtility.UrlDecode(returnUrl));
        }

        public async Task<IActionResult> OnPostPrivateMessage()
        {
            throw await Task.FromResult(new NotImplementedException());
        }

        private string RemoveBbCodeUid(string text, string uid)
        {
            var uidRegex = new Regex($":{uid}", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            var tagRegex = new Regex(@"(:[a-z])(\]|:)", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            var cleanTextTemp = uidRegex.Replace(text, string.Empty);
            return tagRegex.Replace(cleanTextTemp, "$2");
        }

        private async Task Init(forumContext context)
        {
            DbBbCodes = await (from c in context.PhpbbBbcodes
                               where c.DisplayOnPosting == 1
                               select c)
                              .ToListAsync();

            Smilies = await (from s in context.PhpbbSmilies
                             group s by s.SmileyUrl into unique
                             select unique.First())
                            .OrderBy(s => s.SmileyOrder)
                            .ToListAsync();

            Users = await (from u in context.PhpbbUsers
                           where u.UserId != 1 && u.UserType == 0
                           orderby u.Username
                           select KeyValuePair.Create(u.Username, $"[url=\"./User?UserId={u.UserId}\"]{u.Username}[/url]"))
                          .ToListAsync();

            BbCodes = new List<string>(Constants.BBCODES);
            BbCodeHelplines = new Dictionary<string, string>(Constants.BBCODE_HELPLINES);

            foreach (var bbCode in DbBbCodes)
            {
                BbCodes.Add($"[{bbCode.BbcodeTag}]");
                BbCodes.Add($"[/{bbCode.BbcodeTag}]");
                var index = BbCodes.IndexOf($"[{bbCode.BbcodeTag}]");
                BbCodeHelplines.Add($"cb_{index}", bbCode.BbcodeHelpline);
            }
        }
    }
}