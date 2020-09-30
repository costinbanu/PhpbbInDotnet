using Dapper;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using PhpbbInDotnet.DTOs;
using PhpbbInDotnet.Database;
using PhpbbInDotnet.Database.Entities;
using PhpbbInDotnet.Services;
using PhpbbInDotnet.Utilities;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using System.Web;

namespace PhpbbInDotnet.Forum.Pages
{
    [ValidateAntiForgeryToken, ResponseCache(Location = ResponseCacheLocation.None, NoStore = true, Duration = 0)]
    public partial class PostingModel : ModelWithLoggedUser
    {
        [BindProperty, MaxLength(255, ErrorMessage = "Titlul trebuie să aibă maxim 255 caractere.")]
        public string PostTitle { get; set; }
        
        [BindProperty]
        public string PostText { get; set; }
        
        [BindProperty(SupportsGet = true)]
        public int ForumId { get; set; }
        
        [BindProperty(SupportsGet = true)]
        public int? TopicId { get; set; }

        [BindProperty(SupportsGet = true)]
        public int? PageNum { get; set; }

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
        public List<string> DeleteFileDummyForValidation { get; set; }

        [BindProperty]
        public string EditReason { get; set; }

        [BindProperty(SupportsGet = true)]
        public int? ReceiverId { get; set; }

        [BindProperty]
        public string ReceiverName { get; set; }

        [BindProperty(SupportsGet = true)]
        public int? PrivateMessageId { get; set; }

        [BindProperty]
        public List<PhpbbAttachments> Attachments { get; set; }

        [BindProperty]
        public long? PostTime { get; set; }

        [BindProperty]
        public PhpbbTopics CurrentTopic { get; set; }

        [BindProperty]
        public PostingActions Action { get; set; }

        [BindProperty]
        public long? LastPostTime { get; set; }
        
        public PostDto PreviewablePost { get; private set; }
        public PollDto PreviewablePoll { get; private set; }
        public bool ShowAttach { get; private set; } = false;
        public bool ShowPoll { get; private set; } = false;
        public PhpbbForums CurrentForum { get; private set; }
        public bool DraftSavedSuccessfully { get; private set; } = false;

        private readonly PostService _postService;
        private readonly StorageService _storageService;
        private readonly WritingToolsService _writingService;
        private readonly BBCodeRenderingService _renderingService;

        public PostingModel(CommonUtils utils, ForumDbContext context, ForumTreeService forumService, UserService userService, CacheService cacheService, PostService postService, 
            StorageService storageService, WritingToolsService writingService, BBCodeRenderingService renderingService, IConfiguration config, AnonymousSessionCounter sessionCounter)
            : base(context, forumService, userService, cacheService, config, sessionCounter, utils)
        {
            PollExpirationDaysString = "1";
            PollMaxOptions = 1;
            DeleteFileDummyForValidation = new List<string>();
            _postService = postService;
            _storageService = storageService;
            _writingService = writingService;
            _renderingService = renderingService;
        }

        public async Task<string> GetActualCacheKey(string key, bool isPersonalizedData)
            => isPersonalizedData ? $"{(await GetCurrentUserAsync()).UserId}_{ForumId}_{TopicId ?? 0}_{key ?? throw new ArgumentNullException(nameof(key))}" : key;

        public async Task<(IEnumerable<PhpbbPosts> posts, IEnumerable<PhpbbUsers> users)> GetPreviousPosts()
        {
            if (((TopicId.HasValue && PageNum.HasValue) || PostId.HasValue) && (Action == PostingActions.EditForumPost || Action == PostingActions.NewForumPost))
            {
                using var connection = _context.Database.GetDbConnection();
                await connection.OpenIfNeeded();
                var posts = await connection.QueryAsync<PhpbbPosts>("SELECT * FROM phpbb_posts WHERE topic_id = @topicId ORDER BY post_time DESC LIMIT 10", new { TopicId });
                var users = await connection.QueryAsync<PhpbbUsers>("SELECT * FROM phpbb_users WHERE user_id IN @userIds", new { userIds = posts.Select(pp => pp.PosterId).DefaultIfEmpty() });
                return (posts, users);
            }
            return (new List<PhpbbPosts>(), new List<PhpbbUsers>());
        }

        private async Task Init()
        {
            using (var connection = _context.Database.GetDbConnection())
            {
                await connection.OpenIfNeeded();
                var smileys = await connection.QueryAsync<PhpbbSmilies>("SELECT * FROM phpbb_smilies GROUP BY smiley_url ORDER BY smiley_order");
                await _cacheService.SetInCache(await GetActualCacheKey("Smilies", false), smileys.ToList());
            }

            var userMap = await (
                from u in _context.PhpbbUsers.AsNoTracking()
                where u.UserId != Constants.ANONYMOUS_USER_ID && u.UserType != 2
                orderby u.Username
                select KeyValuePair.Create(u.Username, u.UserId)
            ).ToListAsync();
            await _cacheService.SetInCache(
                await GetActualCacheKey("Users", false),
                userMap.Select(map => KeyValuePair.Create(map.Key, $"[url={_config.GetValue<string>("BaseUrl")}/User?UserId={map.Value}]{map.Key}[/url]"))
            );
            await _cacheService.SetInCache(await GetActualCacheKey("UserMap", false), userMap);

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
            await _cacheService.SetInCache(await GetActualCacheKey("BbCodeHelplines", false), helplines);
            await _cacheService.SetInCache(await GetActualCacheKey("BbCodes", false), bbcodes);
            await _cacheService.SetInCache(await GetActualCacheKey("DbBbCodes", false), dbBbCodes);
        }

        private async Task<PhpbbPosts> InitEditedPost()
        {
            var post = await _context.PhpbbPosts.FirstOrDefaultAsync(p => p.PostId == PostId);

            post.PostSubject = HttpUtility.HtmlEncode(PostTitle);
            post.PostEditTime = DateTime.UtcNow.ToUnixTimestamp();
            post.PostEditUser = (await GetCurrentUserAsync()).UserId;
            post.PostEditReason = HttpUtility.HtmlEncode(EditReason ?? string.Empty);
            post.PostEditCount++;

            return post;
        }

        private async Task<int?> UpsertPost(PhpbbPosts post, LoggedUser usr)
        {
            if ((PostTitle?.Trim()?.Length ?? 0) < 3)
            {
                ModelState.AddModelError(nameof(PostTitle), "Titlul este prea scurt (minim 3 caractere, exclusiv spații).");
                return null;
            }

            if ((PostText?.Trim()?.Length ?? 0) < 3)
            {
                ModelState.AddModelError(nameof(PostText), "Mesajul este prea scurt (minim 3 caractere, exclusiv spații).");
                return null;
            }

            var curTopic = Action != PostingActions.NewTopic ? await _context.PhpbbTopics.FirstOrDefaultAsync(t => t.TopicId == TopicId) : null;
            var canCreatePoll = Action == PostingActions.NewTopic || (Action == PostingActions.EditForumPost && (curTopic?.TopicFirstPostId ?? 0) == PostId);
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

            if (Action == PostingActions.NewTopic)
            {
                var topicResult = await _context.PhpbbTopics.AddAsync(new PhpbbTopics
                {
                    ForumId = ForumId,
                    TopicTitle = PostTitle,
                    TopicTime = DateTime.UtcNow.ToUnixTimestamp()
                });
                topicResult.Entity.TopicId = 0;
                await _context.SaveChangesAsync();
                curTopic = topicResult.Entity;
                TopicId = topicResult.Entity.TopicId;
            }

            var newPostText = PostText;
            var uid = string.Empty;
            var bitfield = string.Empty;
            var hasAttachments = Attachments?.Any() ?? false;
            if (_config.GetValue<bool>("CompatibilityMode"))
            {
                (newPostText, uid, bitfield) = _renderingService.TransformForBackwardsCompatibility(newPostText);
            }
            else
            {
                newPostText = HttpUtility.HtmlEncode(newPostText);
            }

            if (post == null)
            {
                var postResult = await _context.PhpbbPosts.AddAsync(new PhpbbPosts
                {
                    ForumId = ForumId,
                    TopicId = TopicId.Value,
                    PosterId = usr.UserId,
                    PostSubject = HttpUtility.HtmlEncode(PostTitle),
                    PostText = _writingService.PrepareTextForSaving(newPostText),
                    PostTime = DateTime.UtcNow.ToUnixTimestamp(),
                    PostApproved = 1,
                    PostReported = 0,
                    BbcodeUid = uid,
                    BbcodeBitfield = bitfield,
                    EnableBbcode = 1,
                    EnableMagicUrl = 1,
                    EnableSig = 1,
                    EnableSmilies = 1,
                    PostAttachment = (byte)(hasAttachments ? 1 : 0),
                    PostChecksum = _utils.CalculateMD5Hash(newPostText),
                    PostEditCount = 0,
                    PostEditLocked = 0,
                    PostEditReason = string.Empty,
                    PostEditTime = 0,
                    PostEditUser = 0,
                    PosterIp = HttpContext.Connection.RemoteIpAddress.ToString(),
                    PostUsername = HttpUtility.HtmlEncode(usr.Username)
                });
                postResult.Entity.PostId = 0;
                await _context.SaveChangesAsync();
                post = postResult.Entity;
                await _postService.CascadePostAdd(_context, post, false);
            }
            else
            {
                post.PostText = _writingService.PrepareTextForSaving(newPostText);
                post.BbcodeUid = uid;
                post.BbcodeBitfield = bitfield;
                post.PostAttachment = (byte)(hasAttachments ? 1 : 0);

                await _context.SaveChangesAsync();
                await _postService.CascadePostEdit(_context, post);
            }
            await _context.SaveChangesAsync();

            using var connection = _context.Database.GetDbConnection();
            await connection.OpenIfNeeded();
            var attachments = (await connection.QueryAsync<PhpbbAttachments>("SELECT * FROM phpbb_attachments WHERE attach_id IN @attachmentIds", new { attachmentIds = Attachments?.Select(a => a.AttachId).DefaultIfEmpty() })).AsList();
            _context.PhpbbAttachments.UpdateRange(attachments);
            for (var i = 0; i < (attachments?.Count ?? 0); i++)
            {
                attachments[i].PostMsgId = post.PostId;
                attachments[i].TopicId = TopicId.Value;
                attachments[i].AttachComment = _writingService.PrepareTextForSaving(Attachments?[i]?.AttachComment ?? string.Empty);
                attachments[i].IsOrphan = 0;
            }
            await _context.SaveChangesAsync();

            if (canCreatePoll && !string.IsNullOrWhiteSpace(PollOptions))
            {
                byte pollOptionId = 1;

                var options = await _context.PhpbbPollOptions.Where(o => o.TopicId == TopicId).ToListAsync();
                if (pollOptionsArray.Intersect(options.Select(x => x.PollOptionText.Trim()), StringComparer.InvariantCultureIgnoreCase).Count() != options.Count)
                {
                    _context.PhpbbPollOptions.RemoveRange(options);
                    _context.PhpbbPollVotes.RemoveRange(await _context.PhpbbPollVotes.Where(v => v.TopicId == TopicId).ToListAsync());
                    await _context.SaveChangesAsync();
                }

                foreach (var option in pollOptionsArray)
                {
                    await connection.ExecuteAsync(
                        "INSERT INTO forum.phpbb_poll_options (poll_option_id, topic_id, poll_option_text, poll_option_total) VALUES (@id, @topicId, @text, 0)",
                        new { id = pollOptionId++, TopicId, text = HttpUtility.HtmlEncode(option)  }
                    );
                }

                curTopic.PollStart = post.PostTime;
                curTopic.PollLength = (int)TimeSpan.FromDays(double.Parse(PollExpirationDaysString)).TotalSeconds;
                curTopic.PollMaxOptions = (byte)(PollMaxOptions ?? 1);
                curTopic.PollTitle = HttpUtility.HtmlEncode(PollQuestion);
                curTopic.PollVoteChange = (byte)(PollCanChangeVote ? 1 : 0);
            }

            await _context.SaveChangesAsync();
            await connection.ExecuteAsync(
                "DELETE FROM forum.phpbb_drafts WHERE user_id = @userId AND forum_id = @forumId AND topic_id = @topicId",
                new { usr.UserId, forumId = ForumId, topicId = Action == PostingActions.NewTopic ? 0 : TopicId }
            );
            await _cacheService.RemoveFromCache(await GetActualCacheKey("Text", true));
            return post.PostId;
        }

        private async Task<IActionResult> WithNewestPostSincePageLoad(PhpbbForums curForum, Func<Task<IActionResult>> toDo)
        {
            if ((TopicId ?? 0) > 0 && (LastPostTime ?? 0) > 0)
            {
                var currentLastPostTime = await _context.PhpbbPosts.AsNoTracking().Where(p => p.TopicId == TopicId).MaxAsync(p => p.PostTime);
                if (currentLastPostTime > LastPostTime)
                {
                    return PageWithError(curForum, nameof(LastPostTime), "De când a fost încărcată pagina, au mai fost scrie mesaje noi! Verifică mesajele precedente!");
                }
                else
                {
                    ModelState[nameof(LastPostTime)].Errors.Clear();
                }
            }
            return await toDo();
        }

        private IActionResult PageWithError(PhpbbForums curForum, string errorKey, string errorMessage)
        {
            ModelState.AddModelError(errorKey, errorMessage);
            CurrentForum = curForum;
            return Page();
        }

        private async Task<IActionResult> WithBackup(Func<Task<IActionResult>> toDo)
        {
            await _cacheService.SetInCache(await GetActualCacheKey("Text", true), new CachedText { Text = PostText, CacheTime = DateTime.UtcNow });
            await _cacheService.SetInCache(await GetActualCacheKey("ForumId", true), ForumId);
            await _cacheService.SetInCache(await GetActualCacheKey("TopicId", true), TopicId);
            await _cacheService.SetInCache(await GetActualCacheKey("PostId", true), PostId);
            return await toDo();
        }

        private async Task RestoreBackupIfAny(DateTime? minCacheAge = null)
        {
            var cachedText = await _cacheService.GetAndRemoveFromCache<CachedText>(await GetActualCacheKey("Text", true));
            if (!string.IsNullOrWhiteSpace(cachedText?.Text) && (cachedText?.CacheTime ?? DateTime.MinValue) > (minCacheAge ?? DateTime.UtcNow))
            {
                PostText = cachedText.Text;
            }
            var cachedForumId = await _cacheService.GetAndRemoveFromCache<int>(await GetActualCacheKey("ForumId", true));
            ForumId = cachedForumId != 0 ? cachedForumId : ForumId;
            TopicId ??= await _cacheService.GetAndRemoveFromCache<int?>(await GetActualCacheKey("TopicId", true));
            PostId ??= await _cacheService.GetAndRemoveFromCache<int?>(await GetActualCacheKey("PostId", true));
        }

        public class CachedText
        {
            public string Text { get; set; }
            public DateTime CacheTime { get; set; }
        }
    }
}