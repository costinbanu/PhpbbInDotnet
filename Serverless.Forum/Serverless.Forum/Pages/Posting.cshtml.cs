using Amazon;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Serverless.Forum.ForumDb;
using Serverless.Forum.Services;
using Serverless.Forum.Utilities;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;

namespace Serverless.Forum.Pages
{
    //https://stackoverflow.com/questions/54963951/aws-lambda-file-upload-to-asp-net-core-2-1-razor-page-is-corrupting-binary
    [ValidateAntiForgeryToken]
    public class PostingModel : ModelWithLoggedUser
    {
        [BindProperty, Required]
        public string PostTitle { get; set; }

        [BindProperty, Required]
        public string PostText { get; set; }

        [BindProperty(SupportsGet = true)]
        public int ForumId { get; set; }

        [BindProperty(SupportsGet = true)]
        public int? TopicId { get; set; }

        [BindProperty(SupportsGet = true)]
        public int? PostId { get; set; }

        [BindProperty]
        public bool CanCreatePoll { get; set; }

        [BindProperty]
        public string PollQuestion { get; set; }

        [BindProperty]
        public string PollOptions { get; set; }

        [BindProperty, Required, RegularExpression("^[0-9]([.,][0-9]{1,3})?$", ErrorMessage = "Valoarea introdusă nu este validă. Valori acceptate: între 0.1 și 365")]
        public string PollExpirationDaysString { get; set; }

        [BindProperty, Required, Range(1, int.MaxValue, ErrorMessage = "Valori valide: între 1 și numărul de opțiuni ale chestionarului.")]
        public int? PollMaxOptions { get; set; }

        [BindProperty]
        public bool PollCanChangeVote { get; set; }

        public string Header { get; private set; }

        private readonly PostService _postService;
        private readonly IAmazonS3 _s3Client;

        public PostingModel(IConfiguration config, Utils utils, ForumTreeService forumService, UserService userService, CacheService cacheService, PostService postService)
            : base(config, utils, forumService, userService, cacheService)
        {
            _s3Client = new AmazonS3Client(_config["AwsS3Key"], _config["AwsS3Secret"], RegionEndpoint.EUCentral1);
            PollExpirationDaysString = "1";
            PollMaxOptions = 1;
            _postService = postService;
        }

        public async Task<IActionResult> OnGetForumPost()
        {
            PhpbbTopics curTopic = null;
            using (var context = new ForumDbContext(_config))
            {
                curTopic = await context.PhpbbTopics.FirstOrDefaultAsync(t => t.TopicId == TopicId);

                var permissionError = await ValidateForumPermissionsResponsesAsync(await context.PhpbbForums.FirstOrDefaultAsync(f => f.ForumId == ForumId), ForumId);
                if (permissionError != null)
                {
                    return permissionError;
                }

                await Init(context);
            }

            if (curTopic == null)
            {
                return NotFound();
            }

            PostTitle = $"{Constants.REPLY}{HttpUtility.HtmlDecode(curTopic.TopicTitle)}";
            Header = HttpUtility.HtmlDecode(curTopic.TopicTitle);
            return Page();
        }

        public async Task<IActionResult> OnGetQuoteForumPost()
        {
            PhpbbPosts curPost = null;
            PhpbbTopics curTopic = null;
            string curAuthor = null;

            using (var context = new ForumDbContext(_config))
            {
                curPost = await context.PhpbbPosts.FirstOrDefaultAsync(p => p.PostId == PostId);

                if (curPost == null)
                {
                    return NotFound();
                }

                curTopic = await context.PhpbbTopics.FirstOrDefaultAsync(t => t.TopicId == curPost.TopicId);

                if (curTopic == null)
                {
                    return NotFound();
                }

                curAuthor = curPost.PostUsername;
                if (string.IsNullOrWhiteSpace(curAuthor))
                {
                    curAuthor = (await context.PhpbbUsers.FirstOrDefaultAsync(u => u.UserId == curPost.PosterId))?.Username ?? "Anonymous";
                }

                var permissionError = await ValidateForumPermissionsResponsesAsync(await context.PhpbbForums.FirstOrDefaultAsync(f => f.ForumId == ForumId), ForumId);
                if (permissionError != null)
                {
                    return permissionError;
                }

                await Init(context);
            }

            var subject = curPost.PostSubject.StartsWith(Constants.REPLY) ? curPost.PostSubject.Substring(Constants.REPLY.Length) : curPost.PostSubject;
            PostText = $"[quote=\"{curAuthor}\"]\n{HttpUtility.HtmlDecode(CleanText(curPost.PostText, curPost.BbcodeUid))}\n[/quote]";
            PostTitle = $"{Constants.REPLY}{HttpUtility.HtmlDecode(curTopic.TopicTitle)}";
            Header = HttpUtility.HtmlDecode(curTopic.TopicTitle);
            return Page();
        }

        public async Task<IActionResult> OnGetNewTopic()
        {
            using (var context = new ForumDbContext(_config))
            {
                var curForum = await context.PhpbbForums.FirstOrDefaultAsync(t => t.ForumId == ForumId);

                var permissionError = await ValidateForumPermissionsResponsesAsync(await context.PhpbbForums.FirstOrDefaultAsync(f => f.ForumId == ForumId), ForumId);
                if (permissionError != null)
                {
                    return permissionError;
                }
                Header = HttpUtility.HtmlDecode(curForum.ForumName);

                await Init(context);
            }
            await _cacheService.SetInCacheAsync(GetActualCacheKey("IsNewTopic", true), true);
            return Page();
        }

        public async Task<IActionResult> OnPostAttachment(IFormFile file, string fileComment)
        {
            if (file == null)
            {
                return Page();
            }

            if (CurrentUserId == 1)
            {
                return RedirectToPage("Login");
            }

            var attachList = (await _cacheService.GetFromCacheAsync<List<PhpbbAttachments>>(GetActualCacheKey("PostAttachments", true))) ?? new List<PhpbbAttachments>();
            var name = $"{CurrentUserId}_{Guid.NewGuid():n}";
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

            attachList.Add(new PhpbbAttachments
            {
                AttachComment = fileComment,
                Extension = Path.GetExtension(file.FileName),
                Filetime = DateTime.UtcNow.ToUnixTimestamp(),
                Filesize = file.Length,
                Mimetype = file.ContentType,
                PhysicalFilename = name,
                RealFilename = Path.GetFileName(file.FileName),
                PosterId = CurrentUserId
            });
            await _cacheService.SetInCacheAsync(GetActualCacheKey("PostAttachments", true), attachList);
            return Page();
        }

        public async Task<IActionResult> OnPostForumPost()
        {
            var usr = await GetCurrentUserAsync();
            if (usr.UserId == 1)
            {
                return Unauthorized();
            }

            using (var context = new ForumDbContext(_config))
            {
                foreach (var sr in from s in context.PhpbbSmilies
                                   select new
                                   {
                                       Regex = new Regex(Regex.Escape(s.Code), RegexOptions.Compiled | RegexOptions.Singleline),
                                       Replacement = $"<img src=\"./images/smilies/{s.SmileyUrl.Trim('/')}\" />"
                                   })
                {
                    PostText = sr.Regex.Replace(PostText, sr.Replacement);
                }

                var urlRegex = new Regex(@"(?:(?:https?|ftp):\/\/|\b(?:[a-z\d]+\.))(?:(?:[^\s()<>]+|\((?:[^\s()<>]+|(?:\([^\s()<>]+\)))?\))+(?:\((?:[^\s()<>]+|(?:\(?:[^\s()<>]+\)))?\)|[^\s`!()\[\]{};:'"".,<>?«»“”‘’]))", RegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.ExplicitCapture);
                foreach (Match match in urlRegex.Matches(PostText))
                {
                    var linkText = match.Value;
                    if (linkText.Length > 48)
                    {
                        linkText = $"{linkText.Substring(0, 40)} ... {linkText.Substring(linkText.Length - 8)}";
                    }
                    PostText = match.Result($"<a href=\"{(match.Value.StartsWith("http") ? match.Value : $"//{match.Value}")}\">{linkText}</a>");
                }

                var isNewTopic = await _cacheService.GetFromCacheAsync<bool>(GetActualCacheKey("IsNewTopic", true));
                if (isNewTopic)
                {
                    var topicResult = await context.PhpbbTopics.AddAsync(new PhpbbTopics
                    {
                        ForumId = ForumId,
                        TopicTitle = PostTitle,
                        TopicTime = DateTime.UtcNow.ToUnixTimestamp()
                    });
                    topicResult.Entity.TopicId = 0;
                    await context.SaveChangesAsync();
                    await _cacheService.RemoveFromCacheAsync(GetActualCacheKey("IsNewTopic", true));
                    TopicId = topicResult.Entity.TopicId;
                }

                var attachList = (await _cacheService.GetFromCacheAsync<List<PhpbbAttachments>>(GetActualCacheKey("PostAttachments", true))) ?? new List<PhpbbAttachments>();

                var postResult = await context.PhpbbPosts.AddAsync(new PhpbbPosts
                {
                    ForumId = ForumId,
                    TopicId = TopicId.Value,
                    PosterId = usr.UserId,
                    PostSubject = HttpUtility.HtmlEncode(PostTitle),
                    PostText = HttpUtility.HtmlEncode(PostText),
                    PostTime = DateTime.UtcNow.ToUnixTimestamp(),
                    PostApproved = 1,
                    PostReported = 0,
                    BbcodeUid = _utils.RandomString(),
                    EnableBbcode = 1,
                    EnableMagicUrl = 1,
                    EnableSig = 1,
                    EnableSmilies = 1,
                    PostAttachment = (byte)(attachList.Any() ? 1 : 0),
                    PostChecksum = _utils.CalculateMD5Hash(HttpUtility.HtmlEncode(PostText)),
                    PostEditCount = 0,
                    PostEditLocked = 0,
                    PostEditReason = string.Empty,
                    PostEditTime = 0,
                    PostEditUser = 0,
                    PosterIp = HttpContext.Connection.RemoteIpAddress.ToString()
                });
                postResult.Entity.PostId = 0;
                await context.SaveChangesAsync();
                attachList.ForEach(a =>
                {
                    a.PostMsgId = postResult.Entity.PostId;
                    a.TopicId = TopicId.Value;
                });

                await _postService.CascadePostAdd(context, postResult.Entity, usr);

                if (CanCreatePoll && !string.IsNullOrWhiteSpace(PollOptions))
                {
                    byte id = 1;
                    foreach (var option in PollOptions.Split(Environment.NewLine))
                    {
                        var result = await context.PhpbbPollOptions.AddAsync(new PhpbbPollOptions
                        {
                            PollOptionId = id++,
                            PollOptionText = option,
                            PollOptionTotal = 0,
                            TopicId = TopicId.Value
                        });
                        result.Entity.Id = 0;
                    }

                    var curTopic = await context.PhpbbTopics.FirstOrDefaultAsync(t => t.TopicId == postResult.Entity.TopicId);
                    curTopic.PollStart = postResult.Entity.PostTime;
                    curTopic.PollLength = (int)TimeSpan.FromDays(double.Parse(PollExpirationDaysString)).TotalSeconds;
                    curTopic.PollMaxOptions = (byte)(PollMaxOptions ?? 1);
                    curTopic.PollTitle = PollQuestion;
                    curTopic.PollVoteChange = (byte)(PollCanChangeVote ? 1 : 0);
                }

                await context.PhpbbAttachments.AddRangeAsync(attachList);
                await context.SaveChangesAsync();
                await _cacheService.RemoveFromCacheAsync(GetActualCacheKey("PostAttachments", true));

                return RedirectToPage($"./ViewTopic?postId={postResult.Entity.PostId}&handler=byPostId");
            }
        }

        public async Task<IActionResult> OnPostPrivateMessage()
        {
            throw await Task.FromResult(new NotImplementedException());
        }

        public string GetActualCacheKey(string key, bool isPersonalizedData)
            => isPersonalizedData ? $"{CurrentUserId}_{ForumId}_{TopicId ?? 0}_{key ?? throw new ArgumentNullException(nameof(key))}" : key;

        private string CleanText(string text, string uid)
        {
            var uidRegex = new Regex($":{uid}", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            var tagRegex = new Regex(@"(:[a-z])(\]|:)", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            var cleanTextTemp = uidRegex.Replace(text, string.Empty);
            var noUid = tagRegex.Replace(cleanTextTemp, "$2");

            var noSmileys = noUid;
            var smileyRegex = new Regex("<!-- s(:?.+?) -->.+?<!-- s:?.+?:? -->", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            var smileyMatches = smileyRegex.Matches(noSmileys);
            try
            {
                foreach (Match m in smileyMatches)
                {
                    noSmileys = noSmileys.Replace(m.Value, m.Groups[1].Value);
                }
            }
            catch { }

            var noLinks = noSmileys;
            var linkRegex = new Regex(@"<!-- m --><a\s+(?:[^>]*?\s+)?href=([""'])(.*?)\1.+?<!-- m -->", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            var linkMatches = linkRegex.Matches(noLinks);
            try
            {
                foreach (Match m in linkMatches)
                {
                    noLinks = noLinks.Replace(m.Value, m.Groups[2].Value);
                }
            }
            catch { }

            return noLinks;
        }

        private async Task Init(ForumDbContext context)
        {
            await _cacheService.SetInCacheAsync(
                GetActualCacheKey("Smilies", false),
                await (
                    from s in context.PhpbbSmilies
                    group s by s.SmileyUrl into unique
                    select unique.First()
                ).OrderBy(s => s.SmileyOrder)
                 .ToListAsync()
             );

            await _cacheService.SetInCacheAsync(
                GetActualCacheKey("Users", false),
                await (
                    from u in context.PhpbbUsers
                    where u.UserId != 1 && u.UserType != 2
                    orderby u.Username
                    select KeyValuePair.Create(u.Username, $"[url=\"./User?UserId={u.UserId}\"]{u.Username}[/url]")
                ).ToListAsync()
            );

            var dbBbCodes = await (
                from c in context.PhpbbBbcodes
                where c.DisplayOnPosting == 1
                select c
            ).ToListAsync();
            var helplines = new Dictionary<string, string>(Constants.BBCODE_HELPLINES);
            var bbcodes = new List<string>(Constants.BBCODES);
            foreach (var bbCode in dbBbCodes)
            {
                bbcodes.Add($"[{bbCode.BbcodeTag}]");
                bbcodes.Add($"[/{bbCode.BbcodeTag}]");
                var index = bbcodes.IndexOf($"[{bbCode.BbcodeTag}]");
                helplines.Add($"cb_{index}", bbCode.BbcodeHelpline);
            }
            await _cacheService.SetInCacheAsync(GetActualCacheKey("BbCodeHelplines", false), helplines);
            await _cacheService.SetInCacheAsync(GetActualCacheKey("BbCodes", false), bbcodes);
            await _cacheService.SetInCacheAsync(GetActualCacheKey("DbBbCodes", false), dbBbCodes);


            CanCreatePoll = !(await context.PhpbbPollOptions.Where(o => o.TopicId == (TopicId ?? 0)).ToListAsync()).Any();
            await _cacheService.SetInCacheAsync(GetActualCacheKey("CanCreatePoll", true), CanCreatePoll);
        }
    }
}