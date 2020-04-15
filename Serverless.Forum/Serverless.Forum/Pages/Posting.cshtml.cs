using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Serverless.Forum.Contracts;
using Serverless.Forum.ForumDb;
using Serverless.Forum.Services;
using Serverless.Forum.Utilities;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using System.Web;

namespace Serverless.Forum.Pages
{
    //https://stackoverflow.com/questions/54963951/aws-lambda-file-upload-to-asp-net-core-2-1-razor-page-is-corrupting-binary
    [ValidateAntiForgeryToken]
    public class PostingModel : ModelWithLoggedUser
    {
        [BindProperty(SupportsGet = true)]
        public string PostTitle { get; set; }

        [BindProperty(SupportsGet = true)]
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

        [BindProperty]
        public string PollExpirationDaysString { get; set; }

        [BindProperty, Required, Range(1, int.MaxValue, ErrorMessage = null)]
        public int? PollMaxOptions { get; set; }

        [BindProperty]
        public bool PollCanChangeVote { get; set; }

        [BindProperty]
        public IEnumerable<IFormFile> Files { get; set; }

        [BindProperty]
        public List<string> FileComment { get; set; }

        [BindProperty]
        public List<string> DeleteFileDummyForValidation { get; set; }

        [BindProperty]
        public string EditReason { get; set; }

        public string Header { get; private set; }

        public PostingActions Action { get; private set; }

        private readonly PostService _postService;
        private readonly StorageService _storageService;

        public PostingModel(IConfiguration config, Utils utils, ForumTreeService forumService, UserService userService, CacheService cacheService, PostService postService, StorageService storageService)
            : base(config, utils, forumService, userService, cacheService)
        {
            PollExpirationDaysString = "1";
            PollMaxOptions = 1;
            FileComment = new List<string>();
            DeleteFileDummyForValidation = new List<string>();
            _postService = postService;
            _storageService = storageService;
        }

        public async Task<IActionResult> OnGetForumPost()
        {
            using var context = new ForumDbContext(_config);
            var curTopic = await context.PhpbbTopics.AsNoTracking().FirstOrDefaultAsync(t => t.TopicId == TopicId);

            var permissionError = await ValidateForumPermissionsResponsesAsync(await context.PhpbbForums.AsNoTracking().FirstOrDefaultAsync(f => f.ForumId == ForumId), ForumId).FirstOrDefaultAsync();
            if (permissionError != null)
            {
                return permissionError;
            }
            CanCreatePoll = false;
            await Init(context);

            if (curTopic == null)
            {
                return NotFound();
            }

            PostTitle = $"{Constants.REPLY}{HttpUtility.HtmlDecode(curTopic.TopicTitle)}";
            Header = HttpUtility.HtmlDecode(curTopic.TopicTitle);
            Action = PostingActions.NewForumPost;
            return Page();
        }

        public async Task<IActionResult> OnGetQuoteForumPost()
        {
            using var context = new ForumDbContext(_config);
            var curPost = await context.PhpbbPosts.AsNoTracking().FirstOrDefaultAsync(p => p.PostId == PostId);

            if (curPost == null)
            {
                return NotFound();
            }

            var curTopic = await context.PhpbbTopics.AsNoTracking().FirstOrDefaultAsync(t => t.TopicId == curPost.TopicId);

            if (curTopic == null)
            {
                return NotFound();
            }

            var curAuthor = curPost.PostUsername;
            if (string.IsNullOrWhiteSpace(curAuthor))
            {
                curAuthor = (await context.PhpbbUsers.AsNoTracking().FirstOrDefaultAsync(u => u.UserId == curPost.PosterId))?.Username ?? "Anonymous";
            }

            var permissionError = await ValidateForumPermissionsResponsesAsync(await context.PhpbbForums.AsNoTracking().FirstOrDefaultAsync(f => f.ForumId == ForumId), ForumId).FirstOrDefaultAsync();
            if (permissionError != null)
            {
                return permissionError;
            }
            CanCreatePoll = false;
            await Init(context);

            PostText = $"[quote=\"{curAuthor}\"]\n{HttpUtility.HtmlDecode(_postService.CleanTextForQuoting(curPost.PostText, curPost.BbcodeUid))}\n[/quote]";
            PostTitle = $"{Constants.REPLY}{HttpUtility.HtmlDecode(curPost.PostSubject)}";
            Header = HttpUtility.HtmlDecode(curTopic.TopicTitle);
            Action = PostingActions.NewForumPost;
            
            return Page();
        }

        public async Task<IActionResult> OnGetNewTopic()
        {
            using var context = new ForumDbContext(_config);
            var curForum = await context.PhpbbForums.AsNoTracking().FirstOrDefaultAsync(t => t.ForumId == ForumId);

            var permissionError = await ValidateForumPermissionsResponsesAsync(curForum, ForumId).FirstOrDefaultAsync();
            if (permissionError != null)
            {
                return permissionError;
            }

            Header = HttpUtility.HtmlDecode(curForum.ForumName);
            CanCreatePoll = true;
            await Init(context);
            await _cacheService.SetInCacheAsync(GetActualCacheKey("IsNewTopic", true), true);
            Action = PostingActions.NewForumPost;
            
            return Page();
        }

        public async Task<IActionResult> OnGetEditPost()
        {
            using var context = new ForumDbContext(_config);
            var curPost = await context.PhpbbPosts.AsNoTracking().FirstOrDefaultAsync(p => p.PostId == PostId);
            var curTopic = await context.PhpbbTopics.AsNoTracking().FirstOrDefaultAsync(t => t.TopicId == TopicId);
            
            var permissionError = await ValidateForumPermissionsResponsesAsync(await context.PhpbbForums.AsNoTracking().FirstOrDefaultAsync(f => f.ForumId == ForumId), ForumId).FirstOrDefaultAsync();
            if (permissionError != null)
            {
                return permissionError;
            }
            de testat asta
            if(!await IsCurrentUserModeratorHereAsync() && DateTime.UtcNow.Subtract(curPost.PostTime.ToUtcTime()).TotalMinutes > (await GetCurrentUserAsync()).PostEditTime)
            {
                return RedirectToPage("ViewTopic", "byPostId", new { PostId });
            }

            CanCreatePoll = (
                await (
                    from p in context.PhpbbPosts
                    where p.TopicId == curPost.TopicId
                    group p by p.PostTime into groups
                    orderby groups.Key ascending
                    select groups.FirstOrDefault()
                ).FirstOrDefaultAsync()
            )?.PostId == PostId;

            await Init(context);
            
            await _cacheService.SetInCacheAsync(
                GetActualCacheKey("PostAttachments", true), 
                await context.PhpbbAttachments.AsNoTracking().Where(a => a.PostMsgId == PostId).ToListAsync()
            );
            var subject = curPost.PostSubject.StartsWith(Constants.REPLY) ? curPost.PostSubject.Substring(Constants.REPLY.Length) : curPost.PostSubject;
            PostText = HttpUtility.HtmlDecode(_postService.CleanTextForQuoting(curPost.PostText, curPost.BbcodeUid));
            PostTitle = HttpUtility.HtmlDecode(curPost.PostSubject);
            Header = HttpUtility.HtmlDecode(curTopic.TopicTitle);
            Action = PostingActions.EditForumPost;

            return Page();
        }

        public async Task<IActionResult> OnPostAddAttachment()
        {
            if (!(Files?.Any() ?? false))
            {
                return Page();
            }

            if (CurrentUserId == 1)
            {
                return RedirectToPage("Login");
            }
            using var context = new ForumDbContext(_config);
            var permissionError = await ValidateForumPermissionsResponsesAsync(await context.PhpbbForums.AsNoTracking().FirstOrDefaultAsync(f => f.ForumId == ForumId), ForumId).FirstOrDefaultAsync();
            if (permissionError != null)
            {
                return permissionError;
            }

            var tooLargeFiles = Files.Where(f => f.Length > 1024 * 1024 * 2);
            if (tooLargeFiles.Any() && !await IsCurrentUserAdminHereAsync())
            {
                ModelState.AddModelError(nameof(Files), $"Următoarele fișiere sunt mai mari de 2MB: {string.Join(",", tooLargeFiles.Select(f => f.FileName))}");
                return Page();
            }

            var attachList = (await _cacheService.GetFromCacheAsync<List<PhpbbAttachments>>(GetActualCacheKey("PostAttachments", true))) ?? new List<PhpbbAttachments>();
            if (attachList.Count + Files.Count() > 10 && !await IsCurrentUserAdminHereAsync())
            {
                ModelState.AddModelError(nameof(Files), "Sunt permise maxim 10 fișiere per mesaj.");
                return Page();
            }

            var (succeeded, failed) = await _storageService.BulkAddAttachments(Files, CurrentUserId);
            attachList.AddRange(succeeded);

            if(failed.Any())
            {
                ModelState.AddModelError(nameof(Files), $"Următoarele fișiere nu au putut fi adăugate, vă rugăm să încercați din nou: {string.Join(",", failed)}");
            }

            await _cacheService.SetInCacheAsync(GetActualCacheKey("PostAttachments", true), attachList);
            return Page();
        }

        public async Task<IActionResult> OnPostDeleteAttachment(int index)
        {
            if (CurrentUserId == 1)
            {
                return RedirectToPage("Login");
            }
            using var context = new ForumDbContext(_config);
            var permissionError = await ValidateForumPermissionsResponsesAsync(await context.PhpbbForums.AsNoTracking().FirstOrDefaultAsync(f => f.ForumId == ForumId), ForumId).FirstOrDefaultAsync();
            if (permissionError != null)
            {
                return permissionError;
            }

            var attachList = (await _cacheService.GetFromCacheAsync<List<PhpbbAttachments>>(GetActualCacheKey("PostAttachments", true))) ?? new List<PhpbbAttachments>();
            var attachment = attachList.ElementAtOrDefault(index);

            if(attachment == null)
            {
                ModelState.AddModelError($"{nameof(DeleteFileDummyForValidation)}[{index}]", "Fișierul nu a putut fi șters, vă rugăm încercați din nou.");
                return Page();
            }

            if (!await _storageService.DeleteFile(attachment.PhysicalFilename))
            {
                ModelState.AddModelError($"{nameof(DeleteFileDummyForValidation)}[{index}]", "Fișierul nu a putut fi șters, vă rugăm încercați din nou.");
            }

            if (!string.IsNullOrWhiteSpace(PostText))
            {
                PostText = PostText.Replace($"[attachment={index}]{attachment.RealFilename}[/attachment]", string.Empty, StringComparison.InvariantCultureIgnoreCase);
                for (int i = index + 1; i < attachList.Count; i++)
                {
                    PostText = PostText.Replace($"[attachment={i}]{attachList[i].RealFilename}[/attachment]", $"[attachment={i - 1}]{attachList[i].RealFilename}[/attachment]", StringComparison.InvariantCultureIgnoreCase);
                }
                ModelState.Remove(nameof(PostText));
            }

            var dbAttach = await context.PhpbbAttachments.FirstOrDefaultAsync(a => a.AttachId == attachList[index].AttachId);
            if (dbAttach != null)
            {
                context.PhpbbAttachments.Remove(dbAttach);
                await context.SaveChangesAsync();
            }

            attachList.RemoveAt(index);
            await _cacheService.SetInCacheAsync(GetActualCacheKey("PostAttachments", true), attachList);
            return Page();
        }

        public async Task<IActionResult> OnPostNewForumPost()
        {
            var usr = await GetCurrentUserAsync();
            using var context = new ForumDbContext(_config);
            if (usr == await _userService.GetAnonymousLoggedUserAsync())
            {
                return Unauthorized();
            }
            var permissionError = await ValidateForumPermissionsResponsesAsync(await context.PhpbbForums.AsNoTracking().FirstOrDefaultAsync(f => f.ForumId == ForumId), ForumId).FirstOrDefaultAsync();
            if (permissionError != null)
            {
                return permissionError;
            }

            var addedPostId = UpsertPost(context, null, usr);

            return RedirectToPage("ViewTopic", "byPostId", new { postId = addedPostId });
        }

        public async Task<IActionResult> OnPostEditForumPost()
        {
            var usr = await GetCurrentUserAsync();
            using var context = new ForumDbContext(_config);
            if (usr == await _userService.GetAnonymousLoggedUserAsync())
            {
                return Unauthorized();
            }
            var permissionError = await ValidateForumPermissionsResponsesAsync(await context.PhpbbForums.AsNoTracking().FirstOrDefaultAsync(f => f.ForumId == ForumId), ForumId).FirstOrDefaultAsync();
            if (permissionError != null)
            {
                return permissionError;
            }

            var post = await context.PhpbbPosts.FirstOrDefaultAsync(p => p.PostId == PostId);

            post.PostText = _postService.PrepareTextForSaving(context, HttpUtility.HtmlEncode(PostText));
            post.PostSubject = HttpUtility.HtmlEncode(PostTitle);
            post.PostEditTime = DateTime.UtcNow.ToUnixTimestamp();
            post.PostEditUser = CurrentUserId;
            post.PostEditReason = EditReason ?? string.Empty;
            post.PostEditCount++;

            var addedPostId = UpsertPost(context, post, usr);

            return RedirectToPage("ViewTopic", "byPostId", new { postId = addedPostId });
        }


        public async Task<IActionResult> OnPostPrivateMessage()
        {
            throw await Task.FromResult(new NotImplementedException());
        }

        public string GetActualCacheKey(string key, bool isPersonalizedData)
            => isPersonalizedData ? $"{CurrentUserId}_{ForumId}_{TopicId ?? 0}_{key ?? throw new ArgumentNullException(nameof(key))}" : key;

        private async Task Init(ForumDbContext context)
        {
            await _cacheService.SetInCacheAsync(
                GetActualCacheKey("Smilies", false),
                await (
                    from s in context.PhpbbSmilies.AsNoTracking()
                    group s by s.SmileyUrl into unique
                    select unique.First()
                ).OrderBy(s => s.SmileyOrder)
                 .ToListAsync()
             );

            await _cacheService.SetInCacheAsync(
                GetActualCacheKey("Users", false),
                await (
                    from u in context.PhpbbUsers.AsNoTracking()
                    where u.UserId != 1 && u.UserType != 2
                    orderby u.Username
                    select KeyValuePair.Create(u.Username, $"[url=\"./User?UserId={u.UserId}\"]{u.Username}[/url]")
                ).ToListAsync()
            );

            var dbBbCodes = await (
                from c in context.PhpbbBbcodes.AsNoTracking()
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

            await _cacheService.SetInCacheAsync(GetActualCacheKey("CanCreatePoll", true), CanCreatePoll);
        }

        private async Task<int?> UpsertPost(ForumDbContext context, PhpbbPosts post, LoggedUser usr)
        {
            if ((PostTitle?.Trim()?.Length ?? 0) < 3)
            {
                ModelState.AddModelError(nameof(PostTitle), "Titlul este prea scurt (minim 3 caractere, exclusiv spații).");
                return null;
            }

            if ((PostText?.Trim()?.Length ?? 0) < 3)
            {
                ModelState.AddModelError(nameof(PostText), "Titlul este prea scurt (minim 3 caractere, exclusiv spații).");
                return null;
            }

            if (CanCreatePoll && !string.IsNullOrWhiteSpace(PollExpirationDaysString) && !double.TryParse(PollExpirationDaysString, out var val) && val < 0.1 && val > 365)
            {
                ModelState.AddModelError(nameof(PollExpirationDaysString), "Valoarea introdusă nu este validă. Valori acceptate: între 0.1 și 365");
                return null;
            }

            var pollOptionsArray = PollOptions?.Split(Environment.NewLine) ?? new string[0];
            if (CanCreatePoll && PollMaxOptions.HasValue && pollOptionsArray.Any() && PollMaxOptions < 1 && PollMaxOptions > pollOptionsArray.Length)
            {
                ModelState.AddModelError(nameof(PollMaxOptions), "Valori valide: între 1 și numărul de opțiuni ale chestionarului.");
                return null;
            }

            PhpbbTopics curTopic = null;
            var isNewTopic = await _cacheService.GetFromCacheAsync<bool>(GetActualCacheKey("IsNewTopic", true));
            CanCreatePoll = await _cacheService.GetFromCacheAsync<bool>(GetActualCacheKey("CanCreatePoll", true));
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
                curTopic = topicResult.Entity;
                TopicId = topicResult.Entity.TopicId;
            }

            var attachList = (await _cacheService.GetFromCacheAsync<List<PhpbbAttachments>>(GetActualCacheKey("PostAttachments", true))) ?? new List<PhpbbAttachments>();

            if (post == null)
            {
                var postResult = await context.PhpbbPosts.AddAsync(new PhpbbPosts
                {
                    ForumId = ForumId,
                    TopicId = TopicId.Value,
                    PosterId = usr.UserId,
                    PostSubject = HttpUtility.HtmlEncode(PostTitle),
                    PostText = _postService.PrepareTextForSaving(context, HttpUtility.HtmlEncode(PostText)),
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
                post = postResult.Entity;

                for (var i = 0; i < attachList.Count; i++)
                {
                    attachList[i].PostMsgId = post.PostId;
                    attachList[i].TopicId = TopicId.Value;
                    attachList[i].AttachComment = FileComment[i] ?? string.Empty;
                }

                await _postService.CascadePostAdd(context, post, usr);

                await context.PhpbbAttachments.AddRangeAsync(attachList);
            }
            else
            {
                await context.PhpbbAttachments.AddRangeAsync(attachList.Where(a => a.AttachId == 0));
                var attachXref = await (
                    from a in context.PhpbbAttachments
                    join al in attachList
                    on a.AttachId equals al.AttachId
                    into joined
                    from j in joined
                    select new { Old = a, NewComment = j.AttachComment }
                ).ToListAsync();
                foreach (var xref in attachXref)
                {
                    xref.Old.AttachComment = xref.NewComment;
                }
                await context.SaveChangesAsync();
            }

            if (CanCreatePoll && !string.IsNullOrWhiteSpace(PollOptions))
            {
                byte pollOptionId = 1;
                ulong id = await context.PhpbbPollOptions.AsNoTracking().MaxAsync(o => o.Id);
                foreach (var option in pollOptionsArray)
                {
                    var result = await context.PhpbbPollOptions.AddAsync(new PhpbbPollOptions
                    {
                        PollOptionId = pollOptionId++,
                        PollOptionText = option,
                        PollOptionTotal = 0,
                        TopicId = TopicId.Value,
                        Id = ++id
                    });
                }

                if (curTopic == null)
                {
                    curTopic = await context.PhpbbTopics.FirstOrDefaultAsync(t => t.TopicId == post.TopicId);
                }
                curTopic.PollStart = post.PostTime;
                curTopic.PollLength = (int)TimeSpan.FromDays(double.Parse(PollExpirationDaysString)).TotalSeconds;
                curTopic.PollMaxOptions = (byte)(PollMaxOptions ?? 1);
                curTopic.PollTitle = PollQuestion;
                curTopic.PollVoteChange = (byte)(PollCanChangeVote ? 1 : 0);
            }

            await context.SaveChangesAsync();
            await _cacheService.RemoveFromCacheAsync(GetActualCacheKey("PostAttachments", true));

            return post.PostId;
        }
    }
}