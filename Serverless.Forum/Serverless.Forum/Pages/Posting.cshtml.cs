using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Serverless.Forum.Contracts;
using Serverless.Forum.ForumDb;
using Serverless.Forum.Pages.CustomPartials;
using Serverless.Forum.Services;
using Serverless.Forum.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;

namespace Serverless.Forum.Pages
{
    //https://stackoverflow.com/questions/54963951/aws-lambda-file-upload-to-asp-net-core-2-1-razor-page-is-corrupting-binary
    [ValidateAntiForgeryToken]
    public class PostingModel : ModelWithLoggedUser
    {
        [BindProperty]
        public string PostTitle { get; set; }
        [BindProperty]
        public string PostText { get; set; }
        [BindProperty(SupportsGet = true)]
        public int ForumId { get; set; }
        [BindProperty(SupportsGet = true)]
        public int? TopicId { get; set; }
        [BindProperty(SupportsGet = true)]
        public int? PostId { get; set; }
        [BindProperty]
        public string PollQuestion { get; set; }
        [BindProperty]
        public string PollOptions { get; set; }
        [BindProperty]
        public string PollExpirationDaysString { get; set; }
        [BindProperty]
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
        public PostDisplay PreviewablePost { get; private set; }
        public PollDisplay PreviewablePoll { get; private set; }
        public bool ShowAttach { get; private set; } = false;
        public bool ShowPoll { get; private set; } = false;
        private readonly PostService _postService;
        private readonly StorageService _storageService;
        private readonly WritingToolsService _writingService;
        private readonly BBCodeRenderingService _renderingService;

        public PostingModel(Utils utils, ForumDbContext context, ForumTreeService forumService, UserService userService, CacheService cacheService,
            PostService postService, StorageService storageService, WritingToolsService writingService, BBCodeRenderingService renderingService)
            : base(utils, context, forumService, userService, cacheService)
        {
            PollExpirationDaysString = "1";
            PollMaxOptions = 1;
            FileComment = new List<string>();
            DeleteFileDummyForValidation = new List<string>();
            _postService = postService;
            _storageService = storageService;
            _writingService = writingService;
            _renderingService = renderingService;
        }

        #region GET

        public async Task<IActionResult> OnGetForumPost()
        {
            var curTopic = await _context.PhpbbTopics.AsNoTracking().FirstOrDefaultAsync(t => t.TopicId == TopicId);
            var curForum = await _context.PhpbbForums.AsNoTracking().FirstOrDefaultAsync(f => f.ForumId == ForumId);
            var permissionError = await ValidateForumPermissionsResponsesAsync(curForum, ForumId).FirstOrDefaultAsync();
            if (permissionError != null)
            {
                return permissionError;
            }
            await Init(PostingActions.NewForumPost, false, HttpUtility.HtmlDecode(curTopic.TopicTitle), curForum.ForumName, curForum.ForumId);

            if (curTopic == null)
            {
                return NotFound();
            }
            PostTitle = $"{Constants.REPLY}{HttpUtility.HtmlDecode(curTopic.TopicTitle)}";
            return Page();
        }

        public async Task<IActionResult> OnGetQuoteForumPost()
        {
            var curPost = await _context.PhpbbPosts.AsNoTracking().FirstOrDefaultAsync(p => p.PostId == PostId);
            var curForum = await _context.PhpbbForums.AsNoTracking().FirstOrDefaultAsync(f => f.ForumId == ForumId);
            var curTopic = await _context.PhpbbTopics.AsNoTracking().FirstOrDefaultAsync(t => t.TopicId == curPost.TopicId);

            if (curPost == null)
            {
                return NotFound();
            }

            if (curTopic == null)
            {
                return NotFound();
            }

            var curAuthor = curPost.PostUsername;
            if (string.IsNullOrWhiteSpace(curAuthor))
            {
                curAuthor = (await _context.PhpbbUsers.AsNoTracking().FirstOrDefaultAsync(u => u.UserId == curPost.PosterId))?.Username ?? "Anonymous";
            }

            var permissionError = await ValidateForumPermissionsResponsesAsync(curForum, ForumId).FirstOrDefaultAsync();
            if (permissionError != null)
            {
                return permissionError;
            }
            await Init(PostingActions.NewForumPost, false, HttpUtility.HtmlDecode(curTopic.TopicTitle), curForum.ForumName, curForum.ForumId);

            var title = HttpUtility.HtmlDecode(curPost.PostSubject);
            PostText = $"[quote=\"{curAuthor}\"]\n{HttpUtility.HtmlDecode(_writingService.CleanTextForQuoting(curPost.PostText, curPost.BbcodeUid))}\n[/quote]";
            PostTitle = title.StartsWith(Constants.REPLY) ? title : $"{Constants.REPLY}{title}";

            return Page();
        }

        public async Task<IActionResult> OnGetNewTopic()
        {
            var curForum = await _context.PhpbbForums.AsNoTracking().FirstOrDefaultAsync(t => t.ForumId == ForumId);

            var permissionError = await ValidateForumPermissionsResponsesAsync(curForum, ForumId).FirstOrDefaultAsync();
            if (permissionError != null)
            {
                return permissionError;
            }

            await Init(PostingActions.NewTopic, true, HttpUtility.HtmlDecode(curForum.ForumName), curForum.ForumName, curForum.ForumId);

            return Page();
        }

        public async Task<IActionResult> OnGetEditPost()
        {
            var curPost = await _context.PhpbbPosts.AsNoTracking().FirstOrDefaultAsync(p => p.PostId == PostId);
            var curTopic = await _context.PhpbbTopics.AsNoTracking().FirstOrDefaultAsync(t => t.TopicId == TopicId);
            var curForum = await _context.PhpbbForums.AsNoTracking().FirstOrDefaultAsync(f => f.ForumId == ForumId);
            var permissionError = await ValidateForumPermissionsResponsesAsync(curForum, ForumId).FirstOrDefaultAsync();
            if (permissionError != null)
            {
                return permissionError;
            }
            //de testat asta
            if (!await IsCurrentUserModeratorHereAsync() && DateTime.UtcNow.Subtract(curPost.PostTime.ToUtcTime()).TotalMinutes > (await GetCurrentUserAsync()).PostEditTime)
            {
                return RedirectToPage("ViewTopic", "byPostId", new { PostId });
            }

            var canCreatePoll = (curTopic.TopicFirstPostId == PostId) && (await IsCurrentUserModeratorHereAsync() || (curPost.PosterId == CurrentUserId && DateTime.UtcNow.Subtract(curPost.PostTime.ToUtcTime()).TotalMinutes <= (await GetCurrentUserAsync()).PostEditTime));

            await Init(PostingActions.EditForumPost, canCreatePoll, HttpUtility.HtmlDecode(curTopic.TopicTitle), curForum.ForumName, curForum.ForumId);

            await _cacheService.SetInCache(
                GetActualCacheKey("PostAttachments", true),
                await _context.PhpbbAttachments.AsNoTracking().Where(a => a.PostMsgId == PostId).ToListAsync()
            );

            await _cacheService.SetInCache(GetActualCacheKey("PostTime", true), curPost.PostTime);

            if (canCreatePoll && curTopic.PollStart > 0)
            {
                var pollOptionsText = await _context.PhpbbPollOptions.AsNoTracking().Where(x => x.TopicId == curTopic.TopicId).Select(x => x.PollOptionText).ToListAsync();
                PollQuestion = curTopic.PollTitle;
                PollOptions = string.Join(Environment.NewLine, pollOptionsText);
                PollCanChangeVote = curTopic.PollVoteChange == 1;
                PollExpirationDaysString = TimeSpan.FromSeconds(curTopic.PollLength).TotalDays.ToString();
                PollMaxOptions = curTopic.PollMaxOptions;
            }

            var subject = curPost.PostSubject.StartsWith(Constants.REPLY) ? curPost.PostSubject.Substring(Constants.REPLY.Length) : curPost.PostSubject;
            PostText = HttpUtility.HtmlDecode(_writingService.CleanTextForQuoting(curPost.PostText, curPost.BbcodeUid));
            PostTitle = HttpUtility.HtmlDecode(curPost.PostSubject);

            return Page();
        }

        #endregion GET

        #region POST Attachment

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
            var permissionError = await ValidateForumPermissionsResponsesAsync(await _context.PhpbbForums.AsNoTracking().FirstOrDefaultAsync(f => f.ForumId == ForumId), ForumId).FirstOrDefaultAsync();
            if (permissionError != null)
            {
                return permissionError;
            }

            var tooLargeFiles = Files.Where(f => f.Length > 1024 * 1024 * 2);
            if (tooLargeFiles.Any() && !await IsCurrentUserAdminHereAsync())
            {
                ModelState.AddModelError(nameof(Files), $"Următoarele fișiere sunt mai mari de 2MB: {string.Join(",", tooLargeFiles.Select(f => f.FileName))}");
                ShowAttach = true;
                return Page();
            }

            var attachList = (await _cacheService.GetFromCache<List<PhpbbAttachments>>(GetActualCacheKey("PostAttachments", true))) ?? new List<PhpbbAttachments>();
            if (attachList.Count + Files.Count() > 10 && !await IsCurrentUserAdminHereAsync())
            {
                ModelState.AddModelError(nameof(Files), "Sunt permise maxim 10 fișiere per mesaj.");
                ShowAttach = true;
                return Page();
            }

            var (succeeded, failed) = await _storageService.BulkAddAttachments(Files, CurrentUserId);
            attachList.AddRange(succeeded);

            if (failed.Any())
            {
                ModelState.AddModelError(nameof(Files), $"Următoarele fișiere nu au putut fi adăugate, vă rugăm să încercați din nou: {string.Join(",", failed)}");
            }
            ShowAttach = true;

            await _cacheService.SetInCache(GetActualCacheKey("PostAttachments", true), attachList);
            return Page();
        }

        public async Task<IActionResult> OnPostDeleteAttachment(int index)
        {
            if (CurrentUserId == 1)
            {
                return RedirectToPage("Login");
            }
            var permissionError = await ValidateForumPermissionsResponsesAsync(await _context.PhpbbForums.AsNoTracking().FirstOrDefaultAsync(f => f.ForumId == ForumId), ForumId).FirstOrDefaultAsync();
            if (permissionError != null)
            {
                return permissionError;
            }

            var attachList = (await _cacheService.GetFromCache<List<PhpbbAttachments>>(GetActualCacheKey("PostAttachments", true))) ?? new List<PhpbbAttachments>();
            var attachment = attachList.ElementAtOrDefault(index);

            if (attachment == null)
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

            var dbAttach = await _context.PhpbbAttachments.FirstOrDefaultAsync(a => a.AttachId == attachList[index].AttachId);
            if (dbAttach != null)
            {
                _context.PhpbbAttachments.Remove(dbAttach);
                await _context.SaveChangesAsync();
            }
            ShowAttach = true;

            attachList.RemoveAt(index);
            await _cacheService.SetInCache(GetActualCacheKey("PostAttachments", true), attachList);
            return Page();
        }

        #endregion POST Attachment

        #region POST Message

        public async Task<IActionResult> OnPostPreview()
        {
            var usr = await GetCurrentUserAsync();
            if (usr == await _userService.GetAnonymousLoggedUserAsync())
            {
                return Unauthorized();
            }
            var permissionError = await ValidateForumPermissionsResponsesAsync(await _context.PhpbbForums.AsNoTracking().FirstOrDefaultAsync(f => f.ForumId == ForumId), ForumId).FirstOrDefaultAsync();
            if (permissionError != null)
            {
                return permissionError;
            }

            if ((PostTitle?.Trim()?.Length ?? 0) < 3)
            {
                ModelState.AddModelError(nameof(PostTitle), "Titlul este prea scurt (minim 3 caractere, exclusiv spații).");
                return Page();
            }

            if ((PostText?.Trim()?.Length ?? 0) < 3)
            {
                ModelState.AddModelError(nameof(PostText), "Titlul este prea scurt (minim 3 caractere, exclusiv spații).");
                return Page();
            }

            var action = await _cacheService.GetFromCache<PostingActions>(GetActualCacheKey("Action", true));
            var currentPost = action == PostingActions.EditForumPost ? await InitEditedPost() : null;
            var userId = action == PostingActions.EditForumPost ? currentPost.PosterId : CurrentUserId;
            var postAuthor = await _context.PhpbbUsers.AsNoTracking().FirstOrDefaultAsync(x => x.UserId == userId);
            var rankId = postAuthor?.UserRank ?? 0;
            var attachments = await _cacheService.GetFromCache<List<PhpbbAttachments>>(GetActualCacheKey("PostAttachments", true)) ?? new List<PhpbbAttachments>();
            var postTime = action == PostingActions.EditForumPost ? (await _cacheService.GetFromCache<long?>(GetActualCacheKey("PostTime", true)))?.ToUtcTime() : DateTime.UtcNow;
            
            PreviewablePost = new PostDisplay
            {
                Attachments = attachments.Select(x => new _AttachmentPartialModel(x, true)).ToList(),
                AuthorColor = postAuthor.UserColour,
                AuthorHasAvatar = !string.IsNullOrWhiteSpace(postAuthor?.UserAvatar),
                AuthorId = postAuthor.UserId,
                AuthorName = postAuthor.Username,
                AuthorRank = (await _context.PhpbbRanks.AsNoTracking().FirstOrDefaultAsync(x => x.RankId == rankId))?.RankTitle,
                AuthorSignature = _renderingService.BbCodeToHtml(postAuthor.UserSig, postAuthor.UserSigBbcodeUid),
                BbcodeUid = _utils.RandomString(),
                PostCreationTime = postTime,
                EditCount = (short)((currentPost?.PostEditCount ?? 0) + 1),
                LastEditReason = currentPost?.PostEditReason,
                LastEditTime = DateTime.UtcNow.ToUnixTimestamp(),
                LastEditUser = (await GetCurrentUserAsync()).Username,
                PostId = currentPost?.PostId ?? 0,
                PostSubject = HttpUtility.HtmlEncode(PostTitle),
                PostText = _writingService.PrepareTextForSaving(HttpUtility.HtmlEncode(PostText))
            };

            if (!string.IsNullOrWhiteSpace(PollOptions))
            {
                PreviewablePoll = new PollDisplay
                {
                    PollTitle = HttpUtility.HtmlEncode(PollQuestion),
                    PollOptions = new List<PollOption>(PollOptions.Split(Environment.NewLine).Select(x => new PollOption { PollOptionText = HttpUtility.HtmlEncode(x) })),
                    VoteCanBeChanged = PollCanChangeVote,
                    PollDurationSecons = (int)TimeSpan.FromDays(double.Parse(PollExpirationDaysString)).TotalSeconds,
                    PollMaxOptions = PollMaxOptions ?? 1,
                    PollStart = PreviewablePost.PostCreationTime ?? DateTime.UtcNow
                };
            }
            await _renderingService.ProcessPosts(new[] { PreviewablePost }, PageContext, HttpContext, true);

            return Page();
        }

        public async Task<IActionResult> OnPostNewForumPost()
        {
            var usr = await GetCurrentUserAsync();
            if (usr == await _userService.GetAnonymousLoggedUserAsync())
            {
                return Unauthorized();
            }
            var permissionError = await ValidateForumPermissionsResponsesAsync(await _context.PhpbbForums.AsNoTracking().FirstOrDefaultAsync(f => f.ForumId == ForumId), ForumId).FirstOrDefaultAsync();
            if (permissionError != null)
            {
                return permissionError;
            }

            var addedPostId = await UpsertPost(_context, null, usr);

            if (addedPostId == null)
            {
                return Page();
            }

            return RedirectToPage("ViewTopic", "byPostId", new { postId = addedPostId });
        }

        public async Task<IActionResult> OnPostEditForumPost()
        {
            var usr = await GetCurrentUserAsync();
            if (usr == await _userService.GetAnonymousLoggedUserAsync())
            {
                return Unauthorized();
            }
            var permissionError = await ValidateForumPermissionsResponsesAsync(await _context.PhpbbForums.AsNoTracking().FirstOrDefaultAsync(f => f.ForumId == ForumId), ForumId).FirstOrDefaultAsync();
            if (permissionError != null)
            {
                return permissionError;
            }

            var addedPostId = await UpsertPost(_context, await InitEditedPost(), usr);

            if (addedPostId == null)
            {
                return Page();
            }

            return RedirectToPage("ViewTopic", "byPostId", new { postId = addedPostId });
        }

        public async Task<IActionResult> OnPostPrivateMessage()
        {
            throw await Task.FromResult(new NotImplementedException());
        }

        #endregion POST Message

        #region Helpers

        public string GetActualCacheKey(string key, bool isPersonalizedData)
            => isPersonalizedData ? $"{CurrentUserId}_{ForumId}_{TopicId ?? 0}_{key ?? throw new ArgumentNullException(nameof(key))}" : key;

        private async Task Init(PostingActions action, bool CanCreatePoll, string Header, string forumName, int forumId)
        {
            await _cacheService.SetInCache(
                GetActualCacheKey("Smilies", false),
                await (
                    from s in _context.PhpbbSmilies.AsNoTracking()
                    group s by s.SmileyUrl into unique
                    select unique.First()
                ).OrderBy(s => s.SmileyOrder)
                 .ToListAsync()
             );

            await _cacheService.SetInCache(
                GetActualCacheKey("Users", false),
                await (
                    from u in _context.PhpbbUsers.AsNoTracking()
                    where u.UserId != 1 && u.UserType != 2
                    orderby u.Username
                    select KeyValuePair.Create(u.Username, $"[url=\"./User?UserId={u.UserId}\"]{u.Username}[/url]")
                ).ToListAsync()
            );

            var dbBbCodes = await (
                from c in _context.PhpbbBbcodes.AsNoTracking()
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
            await _cacheService.SetInCache(GetActualCacheKey("BbCodeHelplines", false), helplines);
            await _cacheService.SetInCache(GetActualCacheKey("BbCodes", false), bbcodes);
            await _cacheService.SetInCache(GetActualCacheKey("DbBbCodes", false), dbBbCodes);
            await _cacheService.SetInCache(GetActualCacheKey("Action", true), action);
            await _cacheService.SetInCache(GetActualCacheKey("Header", true), Header);
            await _cacheService.SetInCache(GetActualCacheKey("ForumName", true), forumName);
            await _cacheService.SetInCache(GetActualCacheKey("ForumId", true), forumId);
            await _cacheService.SetInCache(GetActualCacheKey("CanCreatePoll", true), CanCreatePoll);
        }

        private async Task<PhpbbPosts> InitEditedPost()
        {
            var post = await _context.PhpbbPosts.FirstOrDefaultAsync(p => p.PostId == PostId);

            post.PostText = _writingService.PrepareTextForSaving(HttpUtility.HtmlEncode(PostText));
            post.PostSubject = HttpUtility.HtmlEncode(PostTitle);
            post.PostEditTime = DateTime.UtcNow.ToUnixTimestamp();
            post.PostEditUser = CurrentUserId;
            post.PostEditReason = HttpUtility.HtmlEncode(EditReason ?? string.Empty);
            post.PostEditCount++;

            return post;
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

            var canCreatePoll = await _cacheService.GetFromCache<bool>(GetActualCacheKey("CanCreatePoll", true));
            if (canCreatePoll && (string.IsNullOrWhiteSpace(PollExpirationDaysString) || !double.TryParse(PollExpirationDaysString, out var val) || val < 0 || val > 365))
            {
                ModelState.AddModelError(nameof(PollExpirationDaysString), "Valoarea introdusă nu este validă. Valori acceptate: între 0 și 365");
                ShowPoll = true;
                return null;
            }

            var pollOptionsArray = PollOptions?.Split(Environment.NewLine)?.Select(x => x.Trim()) ?? Enumerable.Empty<string>();
            if (canCreatePoll && (PollMaxOptions == null || (pollOptionsArray.Any() && (PollMaxOptions < 1 || PollMaxOptions > pollOptionsArray.Count()))))
            {
                ModelState.AddModelError(nameof(PollMaxOptions), "Valori valide: între 1 și numărul de opțiuni ale chestionarului.");
                ShowPoll = true;
                return null;
            }

            await _cacheService.RemoveFromCache(GetActualCacheKey("CanCreatePoll", true));

            PhpbbTopics curTopic = null;
            var attachList = await _cacheService.GetAndRemoveFromCache<List<PhpbbAttachments>>(GetActualCacheKey("PostAttachments", true)) ?? new List<PhpbbAttachments>();
            var isNewTopic = await _cacheService.GetAndRemoveFromCache<PostingActions>(GetActualCacheKey("Action", true)) == PostingActions.NewTopic;
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
                curTopic = topicResult.Entity;
                TopicId = topicResult.Entity.TopicId;
            }

            if (post == null)
            {
                var postResult = await context.PhpbbPosts.AddAsync(new PhpbbPosts
                {
                    ForumId = ForumId,
                    TopicId = TopicId.Value,
                    PosterId = usr.UserId,
                    PostSubject = HttpUtility.HtmlEncode(PostTitle),
                    PostText = _writingService.PrepareTextForSaving(HttpUtility.HtmlEncode(PostText)),
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
                    PosterIp = HttpContext.Connection.RemoteIpAddress.ToString(),
                    PostUsername = HttpUtility.HtmlEncode(usr.Username)
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

                await _postService.CascadePostAdd(context, post, usr, isNewTopic);
                await context.PhpbbAttachments.AddRangeAsync(attachList);
            }
            else
            {
                for (var i = 0; i < attachList.Count; i++)
                {
                    attachList[i].PostMsgId = post.PostId;
                    attachList[i].TopicId = TopicId.Value;
                    attachList[i].AttachComment = FileComment[i] ?? string.Empty;
                }
                await context.PhpbbAttachments.AddRangeAsync(attachList.Where(a => a.AttachId == 0));
                var attachXref = await (
                    from a in context.PhpbbAttachments
                    join al in attachList
                    on a.AttachId equals al.AttachId
                    into joined
                    from j in joined
                    select new { Old = a, NewComment = j.AttachComment ?? string.Empty }
                ).ToListAsync();
                foreach (var xref in attachXref)
                {
                    xref.Old.AttachComment = xref.NewComment;
                }

                await _postService.CascadePostEdit(context, post, usr);
            }
            await context.SaveChangesAsync();

            if (canCreatePoll && !string.IsNullOrWhiteSpace(PollOptions))
            {
                byte pollOptionId = 1;
                ulong id = await context.PhpbbPollOptions.AsNoTracking().MaxAsync(o => o.Id);

                var options = await context.PhpbbPollOptions.Where(o => o.TopicId == TopicId).ToListAsync();
                if (pollOptionsArray.Intersect(options.Select(x => x.PollOptionText.Trim()), StringComparer.InvariantCultureIgnoreCase).Count() != options.Count)
                {
                    context.PhpbbPollOptions.RemoveRange(options);
                    context.PhpbbPollVotes.RemoveRange(await context.PhpbbPollVotes.Where(v => v.TopicId == TopicId).ToListAsync());
                    await context.SaveChangesAsync();
                }

                foreach (var option in pollOptionsArray)
                {
                    var result = await context.PhpbbPollOptions.AddAsync(new PhpbbPollOptions
                    {
                        PollOptionId = pollOptionId++,
                        PollOptionText = HttpUtility.HtmlEncode(option),
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
                curTopic.PollTitle = HttpUtility.HtmlEncode(PollQuestion);
                curTopic.PollVoteChange = (byte)(PollCanChangeVote ? 1 : 0);
            }

            await context.SaveChangesAsync();
            await _cacheService.RemoveFromCache(GetActualCacheKey("Header", true));

            return post.PostId;
        }

        #endregion Helpers
    }
}