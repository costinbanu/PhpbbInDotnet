using Dapper;
using LazyCache;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using PhpbbInDotnet.Database;
using PhpbbInDotnet.Database.Entities;
using PhpbbInDotnet.Languages;
using PhpbbInDotnet.Objects;
using PhpbbInDotnet.Services;
using PhpbbInDotnet.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;

namespace PhpbbInDotnet.Forum.Pages
{
    [ValidateAntiForgeryToken]
    public partial class PostingModel : AuthenticatedPageModel
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

        static readonly DateTimeOffset CACHE_EXPIRATION = DateTimeOffset.UtcNow.AddHours(4);

        public PostingModel(CommonUtils utils, ForumDbContext context, ForumTreeService forumService, UserService userService, IAppCache cacheService, PostService postService, 
            StorageService storageService, WritingToolsService writingService, BBCodeRenderingService renderingService, IConfiguration config, AnonymousSessionCounter sessionCounter, LanguageProvider languageProvider)
            : base(context, forumService, userService, cacheService, config, sessionCounter, utils, languageProvider)
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

        public async Task<(IEnumerable<PhpbbPosts> posts, IEnumerable<PhpbbAttachments> attachments, IEnumerable<PhpbbUsers> users)> GetPreviousPosts()
        {
            if (((TopicId.HasValue && PageNum.HasValue) || PostId.HasValue) && (Action == PostingActions.EditForumPost || Action == PostingActions.NewForumPost))
            {
                var connection = Context.Database.GetDbConnection();
                var posts = await connection.QueryAsync<PhpbbPosts>("SELECT * FROM phpbb_posts WHERE topic_id = @topicId ORDER BY post_time DESC LIMIT 10", new { TopicId });
                using var multi = await connection.QueryMultipleAsync(
                    "SELECT * FROM phpbb_attachments WHERE post_msg_id IN @postIds ORDER BY attach_id;" +
                    "SELECT * FROM phpbb_users WHERE user_id IN @userIds;", 
                    new { postIds = posts.Select(pp => pp.PostId).DefaultIfEmpty(), userIds = posts.Select(pp => pp.PosterId).DefaultIfEmpty() }
                );
                var attachments = await multi.ReadAsync<PhpbbAttachments>();
                var users = await multi.ReadAsync<PhpbbUsers>();
                Cache.Add(string.Format(Constants.FORUM_CHECK_OVERRIDE_CACHE_KEY_FORMAT, ForumId), true, TimeSpan.FromSeconds(30));
                return (posts, attachments, users);
            }
            return (new List<PhpbbPosts>(), new List<PhpbbAttachments>(), new List<PhpbbUsers>());
        }

        public async Task<List<PhpbbSmilies>> GetSmilies()
            => (await Context.Database.GetDbConnection().QueryAsync<PhpbbSmilies>("SELECT * FROM phpbb_smilies GROUP BY smiley_url ORDER BY smiley_order")).AsList();

        private List<KeyValuePair<string, int>> _userMap = null;
        public async Task<List<KeyValuePair<string, int>>> GetUserMap()
            => _userMap ??= await (
                from u in Context.PhpbbUsers.AsNoTracking()
                where u.UserId != Constants.ANONYMOUS_USER_ID && u.UserType != 2
                orderby u.Username
                select KeyValuePair.Create(u.Username, u.UserId)
            ).ToListAsync();

        public async Task<IEnumerable<KeyValuePair<string, string>>> GetUsers()
            => (await GetUserMap()).Select(map => KeyValuePair.Create(map.Key, $"[url={Config.GetValue<string>("BaseUrl")}/User?UserId={map.Value}]{map.Key}[/url]")).ToList();

        private async Task<PhpbbPosts> InitEditedPost()
        {
            var connection = Context.Database.GetDbConnection();

            var post = await connection.QueryFirstOrDefaultAsync<PhpbbPosts>("SELECT * FROM phpbb_posts WHERE post_id = @postId", new { PostId });

            post.PostSubject = HttpUtility.HtmlEncode(PostTitle);
            post.PostEditTime = DateTime.UtcNow.ToUnixTimestamp();
            post.PostEditUser = (await GetCurrentUserAsync()).UserId;
            post.PostEditReason = HttpUtility.HtmlEncode(EditReason ?? string.Empty);
            post.PostEditCount++;

            return post;
        }

        private async Task<int?> UpsertPost(PhpbbPosts post, AuthenticatedUser usr)
        {
            var lang = await GetLanguage();

            if ((PostTitle?.Length ?? 0) > 255)
            {
                ModelState.AddModelError(nameof(PostTitle), LanguageProvider.Errors[lang, "TITLE_TOO_LONG"]);
                return null;
            }

            if ((PostTitle?.Trim()?.Length ?? 0) < 3)
            {
                ModelState.AddModelError(nameof(PostTitle), LanguageProvider.Errors[lang, "TITLE_TOO_SHORT"]);
                return null;
            }

            if ((PostText?.Trim()?.Length ?? 0) < 3)
            {
                ModelState.AddModelError(nameof(PostText), LanguageProvider.Errors[lang, "POST_TOO_SHORT"]);
                return null;
            }

            var connection = Context.Database.GetDbConnection();

            var curTopic = Action != PostingActions.NewTopic ? await connection.QuerySingleOrDefaultAsync<PhpbbTopics>("SELECT * FROM phpbb_topics WHERE topic_id = @topicId", new { TopicId }) : null;
            var canCreatePoll = Action == PostingActions.NewTopic || (Action == PostingActions.EditForumPost && (curTopic?.TopicFirstPostId ?? 0) == PostId);
            if (canCreatePoll && (string.IsNullOrWhiteSpace(PollExpirationDaysString) || !double.TryParse(PollExpirationDaysString, out var val) || val < 0 || val > 365))
            {
                ModelState.AddModelError(nameof(PollExpirationDaysString), LanguageProvider.Errors[lang, "INVALID_POLL_EXPIRATION"]);
                ShowPoll = true;
                return null;
            }

            var pollOptionsArray = GetPollOptionsEnumerable();
            if (canCreatePoll && (PollMaxOptions == null || (pollOptionsArray.Any() && (PollMaxOptions < 1 || PollMaxOptions > pollOptionsArray.Count()))))
            {
                ModelState.AddModelError(nameof(PollMaxOptions), LanguageProvider.Errors[lang, "INVALID_POLL_OPTION_COUNT"]);
                ShowPoll = true;
                return null;
            }

            if (Action == PostingActions.NewTopic)
            {
                curTopic = await connection.QueryFirstOrDefaultAsync<PhpbbTopics>(
                    "INSERT INTO phpbb_topics (forum_id, topic_title, topic_time) VALUES (@forumId, @postTitle, @now); " +
                    "SELECT * FROM phpbb_topics WHERE topic_id = LAST_INSERT_ID();", 
                    new { ForumId, PostTitle, now = DateTime.UtcNow.ToUnixTimestamp() }
                );
                TopicId = curTopic.TopicId;
            }

            var newPostText = PostText;
            var uid = string.Empty;
            var bitfield = string.Empty;
            var hasAttachments = Attachments?.Any() ?? false;
            newPostText = HttpUtility.HtmlEncode(newPostText);

            if (post == null)
            {
                post = await connection.QueryFirstOrDefaultAsync<PhpbbPosts>(
                    "INSERT INTO phpbb_posts (forum_id, topic_id, poster_id, post_subject, post_text, post_time, bbcode_uid, bbcode_bitfield, post_attachment, post_checksum, poster_ip, post_username) " +
                        "VALUES (@forumId, @topicId, @userId, @subject, @text, @now, @uid, @bitfield, @attachment, @checksum, @ip, @username); " +
                    "SELECT * FROM phpbb_posts WHERE post_id = LAST_INSERT_ID();",
                    new
                    {
                        ForumId,
                        topicId = TopicId.Value,
                        usr.UserId,
                        subject = HttpUtility.HtmlEncode(PostTitle),
                        text = await _writingService.PrepareTextForSaving(newPostText),
                        now = DateTime.UtcNow.ToUnixTimestamp(),
                        uid,
                        bitfield,
                        attachment = hasAttachments.ToByte(),
                        checksum = Utils.CalculateMD5Hash(newPostText),
                        ip = HttpContext.Connection.RemoteIpAddress.ToString(),
                        username = HttpUtility.HtmlEncode(usr.Username)
                    }
                );

                await _postService.CascadePostAdd(post, false);
            }
            else
            {
                await connection.ExecuteAsync(
                    "UPDATE phpbb_posts " +
                    "SET post_subject = @subject, post_text = @text, bbcode_uid = @uid, bbcode_bitfield = @bitfield, post_attachment = @attachment, post_edit_time = @now, post_edit_reason = @reason, post_edit_user = @userId, post_edit_count = post_edit_count + 1 " +
                    "WHERE post_id = @postId",
                    new 
                    { 
                        subject = post.PostSubject,
                        text = await _writingService.PrepareTextForSaving(newPostText), 
                        uid, 
                        bitfield, 
                        attachment = hasAttachments.ToByte(), 
                        post.PostId,
                        now = DateTime.UtcNow.ToUnixTimestamp(),
                        reason = HttpUtility.HtmlEncode(EditReason ?? string.Empty),
                        usr.UserId
                    }
                );

                await _postService.CascadePostEdit(post);
            }

            await connection.ExecuteAsync(
                "UPDATE phpbb_attachments SET post_msg_id = @postId, topic_id = @topicId, attach_comment = @comment, is_orphan = 0 WHERE attach_id = @attachId",
                Attachments?.Select(a => new { post.PostId, topicId = TopicId.Value, comment = _writingService.PrepareTextForSaving(a.AttachComment).GetAwaiter().GetResult(), a.AttachId })
            );

            if (canCreatePoll && !string.IsNullOrWhiteSpace(PollOptions))
            {
                var existing = await connection.QueryAsync<string>("SELECT LTRIM(RTRIM(poll_option_text)) FROM phpbb_poll_options WHERE topic_id = @topicId", new { TopicId });
                if (!existing.SequenceEqual(pollOptionsArray, StringComparer.InvariantCultureIgnoreCase))
                {
                    await connection.ExecuteAsync(
                        "DELETE FROM phpbb_poll_options WHERE topic_id = @topicId;" +
                        "DELETE FROM phpbb_poll_votes WHERE topic_id = @topicId",
                        new { TopicId }
                    );
                    
                    byte id = 1;
                    foreach (var option in pollOptionsArray)
                    {
                        await connection.ExecuteAsync(
                            "INSERT INTO forum.phpbb_poll_options (poll_option_id, topic_id, poll_option_text, poll_option_total) VALUES (@id, @topicId, @text, 0)",
                            new { id = id++, TopicId, text = HttpUtility.HtmlEncode(option) }
                        );
                    }

                    await connection.ExecuteAsync(
                        "UPDATE phpbb_topics SET poll_start = @start, poll_length = @length, poll_max_options = @maxOptions, poll_title = @title, poll_vote_change = @change WHERE topic_id = @topicId",
                        new
                        {
                            start = curTopic.PollStart == 0 ? DateTime.UtcNow.ToUnixTimestamp() : curTopic.PollStart,
                            length = (int)TimeSpan.FromDays(double.Parse(PollExpirationDaysString)).TotalSeconds,
                            maxOptions = (byte)(PollMaxOptions ?? 1),
                            title = HttpUtility.HtmlEncode(PollQuestion),
                            change = PollCanChangeVote.ToByte(),
                            TopicId
                        }
                    );
                }
            }

            await connection.ExecuteAsync(
                "DELETE FROM forum.phpbb_drafts WHERE user_id = @userId AND forum_id = @forumId AND topic_id = @topicId",
                new { usr.UserId, forumId = ForumId, topicId = Action == PostingActions.NewTopic ? 0 : TopicId }
            );

            Cache.Remove(await GetActualCacheKey("Text", true));
            Cache.Remove(await GetActualCacheKey("ForumId", true));
            Cache.Remove(await GetActualCacheKey("TopicId", true));
            Cache.Remove(await GetActualCacheKey("PostId", true));

            return post.PostId;
        }

        private async Task<IActionResult> WithNewestPostSincePageLoad(PhpbbForums curForum, Func<Task<IActionResult>> toDo)
        {
            var lang = await GetLanguage();

            if ((TopicId ?? 0) > 0 && (LastPostTime ?? 0) > 0)
            {
                var connection = Context.Database.GetDbConnection();
                var times = await connection.QueryFirstOrDefaultAsync(
                    @"SELECT p.post_time, p.post_edit_time
                        FROM phpbb_posts p
                        JOIN phpbb_topics t ON p.post_id = t.topic_last_post_id
                       WHERE t.topic_id = @topicId",
                    new { TopicId }
                );
                if (((long?)times?.post_time ?? 0L) > LastPostTime)
                {
                    return PageWithError(curForum, nameof(LastPostTime), LanguageProvider.Errors[lang, "NEW_MESSAGES_SINCE_LOAD"]);
                }
                else if(((long?)times?.post_edit_time ?? 0L) > LastPostTime)
                {
                    LastPostTime = (long?)times?.post_edit_time;
                    return PageWithError(curForum, nameof(LastPostTime), LanguageProvider.Errors[lang, "LAST_MESSAGE_WAS_EDITED_SINCE_LOAD"]);
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
            Cache.Add(await GetActualCacheKey("Text", true), new CachedText { Text = PostText, CacheTime = DateTime.UtcNow }, CACHE_EXPIRATION);
            Cache.Add(await GetActualCacheKey("ForumId", true), ForumId, CACHE_EXPIRATION);
            Cache.Add(await GetActualCacheKey("TopicId", true), TopicId ?? 0, CACHE_EXPIRATION);
            Cache.Add(await GetActualCacheKey("PostId", true), PostId ?? 0, CACHE_EXPIRATION);
            return await toDo();
        }

        private async Task RestoreBackupIfAny(DateTime? minCacheAge = null)
        {
            var cachedText = await Cache.GetAndRemoveAsync<CachedText>(await GetActualCacheKey("Text", true));
            if (!string.IsNullOrWhiteSpace(cachedText?.Text) && (cachedText?.CacheTime ?? DateTime.MinValue) > (minCacheAge ?? DateTime.UtcNow))
            {
                PostText = cachedText.Text;
            }
            var cachedForumId = await Cache.GetAndRemoveAsync<int>(await GetActualCacheKey("ForumId", true));
            ForumId = cachedForumId != 0 ? cachedForumId : ForumId;
            TopicId ??= await Cache.GetAndRemoveAsync<int?>(await GetActualCacheKey("TopicId", true));
            PostId ??= await Cache.GetAndRemoveAsync<int?>(await GetActualCacheKey("PostId", true));
        }

        private IEnumerable<string> GetPollOptionsEnumerable()
            => PollOptions?.Split('\n', StringSplitOptions.RemoveEmptyEntries)?.Select(x => x.Trim())?.Where(x => !string.IsNullOrWhiteSpace(x)) ?? Enumerable.Empty<string>();

        public class CachedText
        {
            public string Text { get; set; }
            public DateTime CacheTime { get; set; }
        }
    }
}