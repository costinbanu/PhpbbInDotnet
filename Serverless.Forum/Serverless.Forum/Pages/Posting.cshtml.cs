﻿using Amazon;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Serverless.Forum.forum;
using Serverless.Forum.Utilities;
using System;
using System.Collections.Generic;
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
        private readonly IAmazonS3 _s3Client;

        [BindProperty]
        public string PostTitle { get; set; }

        [BindProperty]
        public string PostText { get; set; }

        [BindProperty(SupportsGet = true)]
        public int ForumId { get; set; }

        [BindProperty(SupportsGet = true)]
        public int TopicId { get; set; }

        [BindProperty(SupportsGet = true)]
        public int PostId { get; set; }

        public PostingModel(IConfiguration config, Utils utils) : base(config, utils)
        {
            _s3Client = new AmazonS3Client(_config["AwsS3Key"], _config["AwsS3Secret"], RegionEndpoint.EUCentral1);
        }

        public async Task<IActionResult> OnGetForumPost()
        {
            if ((CurrentUserId ?? 1) == 1)
            {
                return Unauthorized();
            }

            PhpbbTopics curTopic = null;
            using (var context = new forumContext(_config))
            {
                curTopic = await context.PhpbbTopics.FirstOrDefaultAsync(t => t.TopicId == TopicId);

                var permissionError = await ValidatePermissionsResponses(await context.PhpbbForums.FirstOrDefaultAsync(f => f.ForumId == ForumId), ForumId);
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

            await SetInCacheAsync("TopicTitle", curTopic.TopicTitle, true);
            PostTitle = $"{Constants.REPLY}{curTopic.TopicTitle}";

            return Page();
        }

        public async Task<IActionResult> OnGetQuoteForumPost()
        {
            if ((CurrentUserId ?? 1) == 1)
            {
                return Unauthorized();
            }

            PhpbbPosts curPost = null;
            PhpbbTopics curTopic = null;
            string curAuthor = null;

            using (var context = new forumContext(_config))
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

                var permissionError = await ValidatePermissionsResponses(await context.PhpbbForums.FirstOrDefaultAsync(f => f.ForumId == ForumId), ForumId);
                if (permissionError != null)
                {
                    return permissionError;
                }

                await Init(context);
            }

            await SetInCacheAsync("TopicTitle", curTopic.TopicTitle, true);

            var subject = curPost.PostSubject.StartsWith(Constants.REPLY) ? curPost.PostSubject.Substring(Constants.REPLY.Length) : curPost.PostSubject;
            PostTitle = PostTitle = $"{Constants.REPLY}{subject}";
            PostText = $"[quote=\"{curAuthor}\"]\n{HttpUtility.HtmlDecode(CleanText(curPost.PostText, curPost.BbcodeUid))}\n[/quote]";

            return Page();
        }

        public async Task<IActionResult> OnPostAttachment(IFormFile file, string fileComment)
        {
            if (file == null)
            {
                return Page();
            }

            if ((CurrentUserId ?? 1) == 1)
            {
                return RedirectToPage("Login");
            }

            var attachList = (await GetFromCacheAsync<List<PhpbbAttachments>>("PostAttachments")) ?? new List<PhpbbAttachments>();
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

            attachList.Add(new PhpbbAttachments
                {
                    AttachComment = fileComment,
                    Extension = Path.GetExtension(file.FileName),
                    Filetime = DateTime.UtcNow.LocalTimeToTimestamp(),
                    Filesize = file.Length,
                    Mimetype = file.ContentType,
                    PhysicalFilename = name,
                    RealFilename = Path.GetFileName(file.FileName),
                    TopicId = TopicId,
                    PosterId = CurrentUserId.Value
                });
            await SetInCacheAsync("PostAttachments", attachList, true);
            return Page();
        }

        public async Task<IActionResult> OnPostForumPost()
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

                var attachList = (await GetFromCacheAsync<List<PhpbbAttachments>>("PostAttachments")) ?? new List<PhpbbAttachments>();

                var result = await context.PhpbbPosts.AddAsync(new PhpbbPosts
                {
                    ForumId = ForumId,
                    TopicId = TopicId,
                    PosterId = usr.UserId.Value,
                    PostSubject = HttpUtility.HtmlEncode(PostTitle),
                    PostText = HttpUtility.HtmlEncode(PostText),
                    PostTime = DateTime.UtcNow.LocalTimeToTimestamp(),
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

                attachList.ForEach(a => a.PostMsgId = result.Entity.PostId);
                await SetInCacheAsync("PostAttachments", attachList, true);
                await context.PhpbbAttachments.AddRangeAsync(attachList);
                await context.SaveChangesAsync();

                return RedirectToPage($"/ViewTopic?postId={result.Entity.PostId}&handler=byPostId");
            }
        }

        public async Task<IActionResult> OnPostPrivateMessage()
        {
            throw await Task.FromResult(new NotImplementedException());
        }

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

        private async Task Init(forumContext context)
        {
            var dbBbCodes = await (
                from c in context.PhpbbBbcodes
                where c.DisplayOnPosting == 1
                select c
            ).ToListAsync();
            await SetInCacheAsync("DbBbCodes", dbBbCodes);

            await SetInCacheAsync(
                "Smilies", 
                await (
                    from s in context.PhpbbSmilies
                    group s by s.SmileyUrl into unique
                    select unique.First()
                ).OrderBy(s => s.SmileyOrder)
                 .ToListAsync()
             );

            await SetInCacheAsync(
                "Users",
                await (
                    from u in context.PhpbbUsers
                    where u.UserId != 1 && u.UserType != 2
                    orderby u.Username
                    select KeyValuePair.Create(u.Username, $"[url=\"./User?UserId={u.UserId}\"]{u.Username}[/url]")
                ).ToListAsync()
            );

            var helplines = new Dictionary<string, string>(Constants.BBCODE_HELPLINES);
            var bbcodes = new List<string>(Constants.BBCODES);
            foreach (var bbCode in dbBbCodes)
            {
                bbcodes.Add($"[{bbCode.BbcodeTag}]");
                bbcodes.Add($"[/{bbCode.BbcodeTag}]");
                var index = bbcodes.IndexOf($"[{bbCode.BbcodeTag}]");
                helplines.Add($"cb_{index}", bbCode.BbcodeHelpline);
            }
            await SetInCacheAsync("BbCodeHelplines", helplines);
            await SetInCacheAsync("BbCodes", bbcodes);

        }
    }
}