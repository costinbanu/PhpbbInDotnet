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
using PhpbbInDotnet.Objects.Configuration;
using PhpbbInDotnet.Services;
using PhpbbInDotnet.Utilities;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web;

namespace PhpbbInDotnet.Forum.Pages
{
    [ValidateAntiForgeryToken]
    public partial class PostingModel : AuthenticatedPageModel
    {
        [BindProperty]
        public string? PostTitle { get; set; }
        
        [BindProperty]
        public string? PostText { get; set; }
        
        [BindProperty(SupportsGet = true)]
        public int ForumId { get; set; }

        [BindProperty(SupportsGet = true)]
        public int? DestinationTopicId { get; set; }

        [BindProperty(SupportsGet = true)]
        public int? TopicId { get; set; }

        [BindProperty(SupportsGet = true)]
        public int? PageNum { get; set; }

        [BindProperty(SupportsGet = true)]
        public int? PostId { get; set; }

        [BindProperty(SupportsGet = true)]
        public bool QuotePostInDifferentTopic { get; set; }
        
        [BindProperty]
        public string? PollQuestion { get; set; }
        
        [BindProperty]
        public string? PollOptions { get; set; }
        
        [BindProperty]
        public string? PollExpirationDaysString { get; set; }
        
        [BindProperty]
        public int? PollMaxOptions { get; set; }
        
        [BindProperty]
        public bool PollCanChangeVote { get; set; }
        
        [BindProperty]
        public IEnumerable<IFormFile>? Files { get; set; }

        [BindProperty]
        public bool ShouldResize { get; set; } = true;

        [BindProperty]
        public bool ShouldHideLicensePlates { get; set; } = true;
        
        [BindProperty]
        public List<string> DeleteFileDummyForValidation { get; set; }

        [BindProperty]
        public string? EditReason { get; set; }

        [BindProperty(SupportsGet = true)]
        public int? ReceiverId { get; set; }

        [BindProperty]
        public string? ReceiverName { get; set; }

        [BindProperty(SupportsGet = true)]
        public int? PrivateMessageId { get; set; }

        [BindProperty]
        public List<PhpbbAttachments>? Attachments { get; set; }

        [BindProperty]
        public long? PostTime { get; set; }

        [BindProperty]
        public PhpbbTopics? CurrentTopic { get; set; }

        [BindProperty]
        public PostingActions? Action { get; set; }

        [BindProperty]
        public long? LastPostTime { get; set; }

        [BindProperty]
        public string? ReturnUrl { get; set; }

        public PostDto? PreviewablePost { get; private set; }
        public PollDto? PreviewablePoll { get; private set; }
        public bool ShowAttach { get; private set; } = false;
        public bool ShowPoll { get; private set; } = false;
        public PhpbbForums? CurrentForum { get; private set; }
        public bool DraftSavedSuccessfully { get; private set; } = false;
        public Guid? PreviewCorrelationId { get; private set; }

        private readonly PostService _postService;
        private readonly StorageService _storageService;
        private readonly WritingToolsService _writingService;
        private readonly BBCodeRenderingService _renderingService;
        private readonly IConfiguration _config;
        private readonly ExternalImageProcessor _imageProcessorOptions;
        private readonly HttpClient? _imageProcessorClient;

        static readonly DateTimeOffset CACHE_EXPIRATION = DateTimeOffset.UtcNow.AddHours(4);

        public PostingModel(CommonUtils utils, ForumDbContext context, ForumTreeService forumService, UserService userService, IAppCache cacheService, PostService postService, 
            StorageService storageService, WritingToolsService writingService, BBCodeRenderingService renderingService, IConfiguration config, LanguageProvider languageProvider, IHttpClientFactory httpClientFactory)
            : base(context, forumService, userService, cacheService, utils, languageProvider)
        {
            PollExpirationDaysString = "1";
            PollMaxOptions = 1;
            DeleteFileDummyForValidation = new List<string>();
            _postService = postService;
            _storageService = storageService;
            _writingService = writingService;
            _renderingService = renderingService;
            _config = config;
            _imageProcessorOptions = _config.GetObject<ExternalImageProcessor>();
            _imageProcessorClient = _imageProcessorOptions.Api?.Enabled == true ? httpClientFactory.CreateClient(_imageProcessorOptions.Api.ClientName) : null;
        }

        public string GetActualCacheKey(string key, bool isPersonalizedData)
            => isPersonalizedData ? $"{GetCurrentUser().UserId}_{ForumId}_{TopicId ?? 0}_{key ?? throw new ArgumentNullException(nameof(key))}" : key;

        public async Task<(List<PostDto> posts, Dictionary<int, List<AttachmentDto>> attachments, Guid correlationId)> GetPreviousPosts()
        {
            if (((TopicId.HasValue && PageNum.HasValue) || PostId.HasValue) && (Action == PostingActions.EditForumPost || Action == PostingActions.NewForumPost))
            {
                var unsortedPosts = (await _postService.GetPosts(TopicId ?? 0, 1, Constants.DEFAULT_PAGE_SIZE)).AsList();
                var attachmentsTask = (
                    from a in Context.PhpbbAttachments.AsNoTracking()
                    where unsortedPosts.Select(p => p.PostId).Contains(a.PostMsgId)
                    select a).ToListAsync();
                var postsTask = Task.Run(() => unsortedPosts.OrderByDescending(p => p.PostTime).ToList());

                await Task.WhenAll(attachmentsTask, postsTask);

                var posts = await postsTask;
                var attachments = await attachmentsTask;
                var cacheResult = await _postService.CacheAttachmentsAndPrepareForDisplay(attachments, GetLanguage(), posts.Count, false);
                return (posts, cacheResult.Attachments, cacheResult.CorrelationId);
            }
            return (new List<PostDto>(), new Dictionary<int, List<AttachmentDto>>(), Guid.Empty);
        }

        private List<KeyValuePair<string, int>>? _userMap = null;
        public async Task<List<KeyValuePair<string, int>>> GetUserMap()
            => _userMap ??= await UserService.GetUserMap();

        public async Task<IEnumerable<KeyValuePair<string, string>>> GetUsers()
            => (await GetUserMap()).Select(map => KeyValuePair.Create(map.Key, $"[url={_config.GetValue<string>("BaseUrl")}/User?UserId={map.Value}]{map.Key}[/url]")).ToList();

        private async Task<int?> UpsertPost(PhpbbPosts? post, AuthenticatedUserExpanded usr)
        {
            var lang = GetLanguage();

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

            var connection = await Context.GetDbConnectionAsync();

            var curTopic = Action != PostingActions.NewTopic ? await connection.QuerySingleOrDefaultAsync<PhpbbTopics>("SELECT * FROM phpbb_topics WHERE topic_id = @topicId", new { TopicId }) : null;
            var canCreatePoll = Action == PostingActions.NewTopic || (Action == PostingActions.EditForumPost && (curTopic?.TopicFirstPostId ?? 0) == PostId);

            if (curTopic?.TopicStatus == 1 && !await IsCurrentUserModeratorHere(ForumId))
            {
                var key = Action == PostingActions.EditForumPost ? "CANT_EDIT_POST_TOPIC_CLOSED" : "CANT_SUBMIT_POST_TOPIC_CLOSED";
                ModelState.AddModelError(nameof(PostText), LanguageProvider.Errors[lang, key, Casing.FirstUpper]);
                ShowPoll = canCreatePoll;
                return null;
            }
            
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
                    @"INSERT INTO phpbb_topics (forum_id, topic_title, topic_time) VALUES (@forumId, @postTitle, @now); 
                    SELECT * FROM phpbb_topics WHERE topic_id = LAST_INSERT_ID();", 
                    new { ForumId, PostTitle, now = DateTime.UtcNow.ToUnixTimestamp() }
                );
                TopicId = curTopic.TopicId;
            }

            var hasAttachments = Attachments?.Any() ?? false;
            var textForSaving = await _writingService.PrepareTextForSaving(HttpUtility.HtmlEncode(PostText));
            if (post == null)
            {
                post = await connection.QueryFirstOrDefaultAsync<PhpbbPosts>(
                    @"INSERT INTO phpbb_posts (forum_id, topic_id, poster_id, post_subject, post_text, post_time, post_attachment, post_checksum, poster_ip, post_username) 
                        VALUES (@forumId, @topicId, @userId, @subject, @textForSaving, @now, @attachment, @checksum, @ip, @username); 
                      SELECT * FROM phpbb_posts WHERE post_id = LAST_INSERT_ID();",
                    new
                    {
                        ForumId,
                        topicId = TopicId!.Value,
                        usr.UserId,
                        subject = HttpUtility.HtmlEncode(PostTitle),
                        textForSaving,
                        now = DateTime.UtcNow.ToUnixTimestamp(),
                        attachment = hasAttachments.ToByte(),
                        checksum = Utils.CalculateMD5Hash(textForSaving),
                        ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? string.Empty,
                        username = HttpUtility.HtmlEncode(usr.Username)
                    }
                );

                await _postService.CascadePostAdd(post, false);
            }
            else
            {
                post = await connection.QueryFirstOrDefaultAsync<PhpbbPosts>(
                    @"UPDATE phpbb_posts 
                    SET post_subject = @subject, post_text = @textForSaving, post_attachment = @attachment, post_checksum = @checksum, post_edit_time = @now, post_edit_reason = @reason, post_edit_user = @userId, post_edit_count = post_edit_count + 1 
                    WHERE post_id = @postId; 
                    SELECT * FROM phpbb_posts WHERE post_id = @postId; ",
                    new
                    {
                        subject = HttpUtility.HtmlEncode(PostTitle),
                        textForSaving,
                        checksum = Utils.CalculateMD5Hash(textForSaving),
                        attachment = hasAttachments.ToByte(),
                        post.PostId,
                        now = DateTime.UtcNow.ToUnixTimestamp(),
                        reason = HttpUtility.HtmlEncode(EditReason ?? string.Empty),
                        usr.UserId
                    }
                );

                await _postService.CascadePostEdit(post);
            }

            foreach (var attach in Attachments!)
            {
                await connection.ExecuteAsync(
                    "UPDATE phpbb_attachments SET post_msg_id = @postId, topic_id = @topicId, attach_comment = @comment, is_orphan = 0 WHERE attach_id = @attachId",
                    new
                    {
                        post.PostId,
                        post.TopicId,
                        comment = await _writingService.PrepareTextForSaving(attach.AttachComment),
                        attach.AttachId
                    });
            }

            if (canCreatePoll && !string.IsNullOrWhiteSpace(PollOptions))
            {
                var existing = await connection.QueryAsync<string>("SELECT LTRIM(RTRIM(poll_option_text)) FROM phpbb_poll_options WHERE topic_id = @topicId", new { TopicId });
                if (!existing.SequenceEqual(pollOptionsArray, StringComparer.InvariantCultureIgnoreCase))
                {
                    await connection.ExecuteAsync(
                        @"DELETE FROM phpbb_poll_options WHERE topic_id = @topicId;
                          DELETE FROM phpbb_poll_votes WHERE topic_id = @topicId",
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
                            start = curTopic!.PollStart == 0 ? DateTime.UtcNow.ToUnixTimestamp() : curTopic.PollStart,
                            length = (int)TimeSpan.FromDays(double.TryParse(PollExpirationDaysString, out var exp) ? exp : 1d).TotalSeconds,
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

            Cache.Remove(GetActualCacheKey("Text", true));
            Cache.Remove(GetActualCacheKey("ForumId", true));
            Cache.Remove(GetActualCacheKey("TopicId", true));
            Cache.Remove(GetActualCacheKey("PostId", true));
            Cache.Remove(GetActualCacheKey("Attachments", true));

            return post.PostId;
        }

        private async Task<IActionResult> WithNewestPostSincePageLoad(PhpbbForums curForum, Func<Task<IActionResult>> toDo)
        {
            var lang = GetLanguage();

            if ((TopicId ?? 0) > 0 && (LastPostTime ?? 0) > 0)
            {
                var connection = await Context.GetDbConnectionAsync();
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

        private IActionResult PageWithError(PhpbbForums? curForum, string errorKey, string errorMessage)
        {
            ModelState.AddModelError(errorKey, errorMessage);
            CurrentForum = curForum;
            return Page();
        }

        private Task<IActionResult> WithBackup(Func<Task<IActionResult>> toDo)
        {
            Cache.Add(GetActualCacheKey("Text", true), new CachedText { Text = PostText!, CacheTime = DateTime.UtcNow }, CACHE_EXPIRATION);
            Cache.Add(GetActualCacheKey("ForumId", true), ForumId, CACHE_EXPIRATION);
            Cache.Add(GetActualCacheKey("TopicId", true), TopicId ?? 0, CACHE_EXPIRATION);
            Cache.Add(GetActualCacheKey("PostId", true), PostId ?? 0, CACHE_EXPIRATION);
            if (Attachments?.Any() == true)
            {
                Cache.Add(GetActualCacheKey("Attachments", true), Attachments?.Select(a => a.AttachId).ToList(), CACHE_EXPIRATION);
            }
            return toDo();
        }

        private async Task RestoreBackupIfAny(DateTime? minCacheAge = null)
        {
            var textTask = Cache.GetAndRemoveAsync<CachedText>(GetActualCacheKey("Text", true));
            var forumIdTask = Cache.GetAndRemoveAsync<int>(GetActualCacheKey("ForumId", true));
            var topicIdTask = Cache.GetAndRemoveAsync<int?>(GetActualCacheKey("TopicId", true));
            var postIdTask = Cache.GetAndRemoveAsync<int?>(GetActualCacheKey("PostId", true));
            var attachmentsTask = Cache.GetAndRemoveAsync<List<int>>(GetActualCacheKey("Attachments", true));
            await Task.WhenAll(textTask, forumIdTask, topicIdTask, postIdTask, attachmentsTask);

            var cachedText = await textTask;
            if ((!string.IsNullOrWhiteSpace(cachedText?.Text) && string.IsNullOrWhiteSpace(PostText)) 
                || (!string.IsNullOrWhiteSpace(cachedText?.Text) && (cachedText?.CacheTime ?? DateTime.MinValue) > (minCacheAge ?? DateTime.UtcNow)))
            {
                PostText = cachedText!.Text;
            }
            var cachedForumId = await forumIdTask;
            ForumId = cachedForumId != 0 ? cachedForumId : ForumId;
            TopicId ??= await topicIdTask;
            PostId ??= await postIdTask;

            var cachedAttachmentIds = await attachmentsTask;
            if (Attachments?.Any() != true && cachedAttachmentIds?.Any() == true)
            {
                var conn = await Context.GetDbConnectionAsync();
                Attachments = (await conn.QueryAsync<PhpbbAttachments>("SELECT * FROM phpbb_attachments WHERE attach_id IN @cachedAttachmentIds", new { cachedAttachmentIds }))?.AsList();
            }
        }

        private IEnumerable<string> GetPollOptionsEnumerable()
            => PollOptions?.Split('\n', StringSplitOptions.RemoveEmptyEntries)?.Select(x => x.Trim())?.Where(x => !string.IsNullOrWhiteSpace(x)) ?? Enumerable.Empty<string>();

        public class CachedText
        {
            public string? Text { get; set; }
            public DateTime CacheTime { get; set; }
        }
    }
}