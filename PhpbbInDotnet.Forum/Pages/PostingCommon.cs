using Dapper;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using PhpbbInDotnet.Objects;
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
    [ValidateAntiForgeryToken]
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

        public async Task<(IEnumerable<PhpbbPosts> posts, IEnumerable<PhpbbAttachments> attachments, IEnumerable<PhpbbUsers> users)> GetPreviousPosts()
        {
            if (((TopicId.HasValue && PageNum.HasValue) || PostId.HasValue) && (Action == PostingActions.EditForumPost || Action == PostingActions.NewForumPost))
            {
                var connection = await _context.GetDbConnectionAndOpenAsync();
                var posts = await connection.QueryAsync<PhpbbPosts>("SELECT * FROM phpbb_posts WHERE topic_id = @topicId ORDER BY post_time DESC LIMIT 10", new { TopicId });
                using var multi = await connection.QueryMultipleAsync(
                    "SELECT * FROM phpbb_attachments WHERE post_msg_id IN @postIds ORDER BY attach_id;" +
                    "SELECT * FROM phpbb_users WHERE user_id IN @userIds;", 
                    new { postIds = posts.Select(pp => pp.PostId).DefaultIfEmpty(), userIds = posts.Select(pp => pp.PosterId).DefaultIfEmpty() }
                );
                var attachments = await multi.ReadAsync<PhpbbAttachments>();
                var users = await multi.ReadAsync<PhpbbUsers>();
                return (posts, attachments, users);
            }
            return (new List<PhpbbPosts>(), new List<PhpbbAttachments>(), new List<PhpbbUsers>());
        }

        private async Task Init()
        {
            var connection = await _context.GetDbConnectionAndOpenAsync();
            var smileys = await connection.QueryAsync<PhpbbSmilies>("SELECT * FROM phpbb_smilies GROUP BY smiley_url ORDER BY smiley_order");
            await _cacheService.SetInCache(await GetActualCacheKey("Smilies", false), smileys.ToList());

            var userMap = (await connection.QueryAsync<PhpbbUsers>(
                "SELECT * FROM phpbb_users WHERE user_id <> @anon AND user_type <> 2 ORDER BY username", 
                new { anon = Constants.ANONYMOUS_USER_ID })
            ).Select(u => KeyValuePair.Create(u.Username, u.UserId)).ToList();

            await _cacheService.SetInCache(
                await GetActualCacheKey("Users", false),
                userMap.Select(map => KeyValuePair.Create(map.Key, $"[url={_config.GetValue<string>("BaseUrl")}/User?UserId={map.Value}]{map.Key}[/url]"))
            );

            await _cacheService.SetInCache(await GetActualCacheKey("UserMap", false), userMap);
        }

        private async Task<PhpbbPosts> InitEditedPost()
        {
            var connection = await _context.GetDbConnectionAndOpenAsync();

            var post = await connection.QueryFirstOrDefaultAsync<PhpbbPosts>("SELECT * FROM phpbb_posts WHERE post_id = @postId", new { PostId });

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

            var connection = await _context.GetDbConnectionAndOpenAsync();

            var curTopic = Action != PostingActions.NewTopic ? await connection.QuerySingleOrDefaultAsync<PhpbbTopics>("SELECT * FROM phpbb_topics WHERE topic_id = @topicId", new { TopicId }) : null;
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
                        checksum = _utils.CalculateMD5Hash(newPostText),
                        ip = HttpContext.Connection.RemoteIpAddress.ToString(),
                        username = HttpUtility.HtmlEncode(usr.Username)
                    }
                );

                await _postService.CascadePostAdd(post, false);
            }
            else
            {
                await connection.ExecuteAsync(
                    "UPDATE phpbb_posts SET post_text = @text, bbcode_uid = @uid, bbcode_bitfield = @bitfield, post_attachment = @attachment WHERE post_id = @postId",
                    new { text = await _writingService.PrepareTextForSaving(newPostText), uid, bitfield, attachment = hasAttachments.ToByte(), post.PostId }
                );

                await _postService.CascadePostEdit(post);
            }

            await connection.ExecuteAsync(
                "UPDATE phpbb_attachments SET post_msg_id = @postId, topic_id = @topicId, attach_comment = @comment, is_orphan = 0 WHERE attach_id = @attachId",
                Attachments?.Select(a => new { post.PostId, topicId = TopicId.Value, comment = _writingService.PrepareTextForSaving(a.AttachComment).RunSync(), a.AttachId })
            );

            if (canCreatePoll && !string.IsNullOrWhiteSpace(PollOptions))
            {
                var existing = await connection.QueryAsync<string>("SELECT LTRIM(RTRIM(poll_option_text)) FROM phpbb_poll_options WHERE topic_id = @topicId", new { TopicId });
                var shouldInsertOptions = Action == PostingActions.NewForumPost || Action == PostingActions.NewTopic;
                if (existing.Any() && !existing.SequenceEqual(pollOptionsArray, StringComparer.InvariantCultureIgnoreCase))
                {
                    shouldInsertOptions = true;
                    await connection.ExecuteAsync(
                        "DELETE FROM phpbb_poll_options WHERE topic_id = @topicId;" +
                        "DELETE FROM phpbb_poll_votes WHERE topic_id = @topicId",
                        new { TopicId }
                    );
                }

                if (shouldInsertOptions)
                {
                    byte id = 1;
                    foreach (var option in pollOptionsArray)
                    {
                        await connection.ExecuteAsync(
                            "INSERT INTO forum.phpbb_poll_options (poll_option_id, topic_id, poll_option_text, poll_option_total) VALUES (@id, @topicId, @text, 0)",
                            new { id = id++, TopicId, text = HttpUtility.HtmlEncode(option) }
                        );
                    }
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

            await connection.ExecuteAsync(
                "DELETE FROM forum.phpbb_drafts WHERE user_id = @userId AND forum_id = @forumId AND topic_id = @topicId",
                new { usr.UserId, forumId = ForumId, topicId = Action == PostingActions.NewTopic ? 0 : TopicId }
            );

            await _cacheService.RemoveFromCache(await GetActualCacheKey("Text", true));
            await _cacheService.RemoveFromCache(await GetActualCacheKey("ForumId", true));
            await _cacheService.RemoveFromCache(await GetActualCacheKey("TopicId", true));
            await _cacheService.RemoveFromCache(await GetActualCacheKey("PostId", true));

            return post.PostId;
        }

        private async Task<IActionResult> WithNewestPostSincePageLoad(PhpbbForums curForum, Func<Task<IActionResult>> toDo)
        {
            if ((TopicId ?? 0) > 0 && (LastPostTime ?? 0) > 0)
            {
                var connection = await _context.GetDbConnectionAndOpenAsync();
                var currentLastPostTime = await connection.ExecuteScalarAsync<long>("SELECT MAX(post_time) FROM phpbb_posts WHERE topic_id = @topicId", new { TopicId });
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