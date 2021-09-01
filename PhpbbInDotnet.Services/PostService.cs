using Dapper;
using LazyCache;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using PhpbbInDotnet.Database;
using PhpbbInDotnet.Database.Entities;
using PhpbbInDotnet.Objects;
using PhpbbInDotnet.Objects.Configuration;
using PhpbbInDotnet.Utilities;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;

namespace PhpbbInDotnet.Services
{
    public class PostService
    {
        private readonly ForumDbContext _context;
        private readonly UserService _userService;
        private readonly StorageService _storageService;
        private readonly IAppCache _cache;
        private readonly CommonUtils _utils;
        private readonly int _maxAttachmentCount;

        public PostService(ForumDbContext context, UserService userService, StorageService storageService, IAppCache cache, CommonUtils utils, IConfiguration config)
        {
            _context = context;
            _userService = userService;
            _storageService = storageService;
            _cache = cache;
            _utils = utils;
            var countLimit = config.GetObject<AttachmentLimits>("UploadLimitsCount");
            _maxAttachmentCount = Math.Max(countLimit.Images, countLimit.OtherFiles);
        }

        public async Task<(Guid CorrelationId, Dictionary<int, List<AttachmentDto>> Attachments)> CacheAttachmentsAndPrepareForDisplay(List<PhpbbAttachments> dbAttachments, string language, int postCount, bool isPreview)
        {
            var correlationId = Guid.NewGuid();
            var attachments = new Dictionary<int, List<AttachmentDto>>(postCount);
            var ids = new List<int>(dbAttachments.Count);
            foreach (var attach in dbAttachments)
            {
                var dto = new AttachmentDto(attach, isPreview, language, correlationId);
                if (!attachments.ContainsKey(attach.PostMsgId))
                {
                    attachments.Add(attach.PostMsgId, new List<AttachmentDto>(_maxAttachmentCount) { dto });
                }
                else
                {
                    attachments[attach.PostMsgId].Add(dto);
                }
                if (attach.Mimetype.IsMimeTypeInline())
                {
                    _cache.Add(_utils.GetAttachmentCacheKey(attach.AttachId, correlationId), dto, TimeSpan.FromSeconds(60));
                    ids.Add(attach.AttachId);
                }
            }

            if (!isPreview && ids.Any())
            {
                try
                {
                    var conn = await _context.GetDbConnectionAsync();
                    await conn.ExecuteAsync(
                        "UPDATE phpbb_attachments SET download_count = download_count + 1 WHERE attach_id IN @ids",
                        new { ids }
                    );
                }
                catch (Exception ex)
                {
                    _utils.HandleErrorAsWarning(ex, "Error updating attachment download count");
                }
            }

            return (correlationId, attachments);
        }

        public async Task<PollDto> GetPoll(PhpbbTopics _currentTopic)
        {
            var options = Enumerable.Empty<PhpbbPollOptions>();
            var voters = Enumerable.Empty<PollOptionVoter>();

            var connection = await _context.GetDbConnectionAsync();
            
            options = await connection.QueryAsync<PhpbbPollOptions>("SELECT * FROM phpbb_poll_options WHERE topic_id = @TopicId ORDER BY poll_option_id", new { _currentTopic.TopicId });
            if (options.Any())
            {
                voters = await connection.QueryAsync<PollOptionVoter>(
                    @"SELECT u.user_id, u.username, v.poll_option_id
                        FROM phpbb_users u
                        JOIN phpbb_poll_votes v ON u.user_id = v.vote_user_id
                        WHERE v.poll_option_id IN @optionIds
                            AND v.topic_id IN @topicIds",
                    new { optionIds = options.Select(o => o.PollOptionId).DefaultIfEmpty(), topicIds = options.Select(o => o.TopicId).DefaultIfEmpty() }
                );
            }
            else
            {
                return null;
            }

            return new PollDto
            {
                PollTitle = _currentTopic.PollTitle,
                PollStart = _currentTopic.PollStart.ToUtcTime(),
                PollDurationSecons = _currentTopic.PollLength,
                PollMaxOptions = _currentTopic.PollMaxOptions,
                TopicId = _currentTopic.TopicId,
                VoteCanBeChanged = _currentTopic.PollVoteChange.ToBool(),
                PollOptions = options.Select(o => 
                new PollOption
                {
                    PollOptionId = o.PollOptionId,
                    PollOptionText = o.PollOptionText,
                    TopicId = o.TopicId,
                    PollOptionVoters = voters.Where(v => v.PollOptionId == o.PollOptionId).ToList()
                }).ToList()
            };
        }

        public async Task CascadePostEdit(PhpbbPosts added)
        {
            var conn = await _context.GetDbConnectionAsync();
            
            var curTopic = await conn.QueryFirstOrDefaultAsync<PhpbbTopics>("SELECT * FROM phpbb_topics WHERE topic_id = @topicId", new { added.TopicId });
            var curForum = await conn.QueryFirstOrDefaultAsync<PhpbbForums>("SELECT * FROM phpbb_forums WHERE forum_id = @forumId", new { curTopic.ForumId });
            var usr = await _userService.GetAuthenticatedUserById(added.PosterId);

            if (curTopic.TopicFirstPostId == added.PostId)
            {
                await SetTopicFirstPost(curTopic, added, usr, true);
            }

            if (curTopic.TopicLastPostId == added.PostId)
            {
                await SetTopicLastPost(curTopic, added, usr);
            }

            if (curForum.ForumLastPostId == added.PostId)
            {
                await SetForumLastPost(curForum, added, usr);
            }
        }

        public async Task CascadePostAdd(PhpbbPosts added, bool ignoreTopic)
        {
            var conn = await _context.GetDbConnectionAsync();
            
            var curTopic = await conn.QueryFirstOrDefaultAsync<PhpbbTopics>("SELECT * FROM phpbb_topics WHERE topic_id = @topicId", new { added.TopicId });
            var curForum = await conn.QueryFirstOrDefaultAsync<PhpbbForums>("SELECT * FROM phpbb_forums WHERE forum_id = @forumId", new { curTopic.ForumId });
            var usr = await _userService.GetAuthenticatedUserById(added.PosterId);

            await SetForumLastPost(curForum, added, usr);

            if (!ignoreTopic)
            {
                await SetTopicLastPost(curTopic, added, usr);
                await SetTopicFirstPost(curTopic, added, usr, false);
            }

            await conn.ExecuteAsync(
                "UPDATE phpbb_topics SET topic_replies = topic_replies + 1, topic_replies_real = topic_replies_real + 1 WHERE topic_id = @topicId; " +
                "UPDATE phpbb_users SET user_posts = user_posts + 1 WHERE user_id = @userId",
                new { curTopic.TopicId, usr.UserId }
            );
        }

        public async Task CascadePostDelete(PhpbbPosts deleted, bool ignoreTopic, bool ignoreAttachmentsAndReports/*, int? oldTopicId = null*/)
        {
            //oldTopicId ??= deleted.TopicId;
            var conn = await _context.GetDbConnectionAsync();
            
            var curTopic = await conn.QueryFirstOrDefaultAsync<PhpbbTopics>("SELECT * FROM phpbb_topics WHERE topic_id = @topicId", new { deleted.TopicId });
            if (curTopic != null)
            {
                //if (await conn.ExecuteScalarAsync<long>("SELECT COUNT(1) FROM phpbb_posts WHERE topic_id = @oldTopicId", new { oldTopicId }) == 0L && !ignoreTopic)
                //{
                //    await conn.ExecuteAsync("DELETE FROM phpbb_topics WHERE topic_id = @topicId", new { curTopic.TopicId });
                //}
                //else
                //{
                    if (curTopic.TopicLastPostId == deleted.PostId && !ignoreTopic)
                    {
                        var lastTopicPost = await conn.QueryFirstOrDefaultAsync<PhpbbPosts>(
                            "SELECT * FROM phpbb_posts WHERE topic_id = @topicId AND post_id <> @postId ORDER BY post_time DESC",
                            new { curTopic.TopicId, deleted.PostId }
                        );
                        var lastTopicPostUser = await _userService.GetAuthenticatedUserById(lastTopicPost.PosterId);

                        await SetTopicLastPost(curTopic, lastTopicPost, lastTopicPostUser, true);
                    }

                    if (curTopic.TopicFirstPostId == deleted.PostId && !ignoreTopic)
                    {
                        var firstPost = await conn.QueryFirstOrDefaultAsync<PhpbbPosts>(
                            "SELECT * FROM phpbb_posts WHERE topic_id = @topicId AND post_id <> @postId ORDER BY post_time ASC",
                            new { /*oldTopicId = */deleted.TopicId, deleted.PostId }
                        );
                        var firstPostUser = await _userService.GetAuthenticatedUserById(firstPost.PosterId);

                        await SetTopicFirstPost(curTopic, firstPost, firstPostUser, false, true);
                    }

                    if (!ignoreTopic)
                    {
                        await conn.ExecuteAsync(
                            "UPDATE phpbb_topics SET topic_replies = GREATEST(topic_replies - 1, 0), topic_replies_real = GREATEST(topic_replies_real - 1, 0) WHERE topic_id = @topicId",
                            new { curTopic.TopicId }
                        );
                    }
                //}
            }

            if (!ignoreAttachmentsAndReports)
            {
                await conn.ExecuteAsync(
                    "DELETE FROM phpbb_reports WHERE post_id = @postId; " +
                    "DELETE FROM phpbb_attachments WHERE post_msg_id = @postId", 
                    new { deleted.PostId }
                );
            }

            var curForum = await conn.QueryFirstOrDefaultAsync<PhpbbForums>("SELECT * FROM phpbb_forums WHERE forum_id = @forumId", new { forumId = curTopic?.ForumId ?? deleted.ForumId });
            if (curForum != null && curForum.ForumLastPostId == deleted.PostId)
            {
                var lastForumPost = await conn.QueryFirstOrDefaultAsync<PhpbbPosts>(
                    "SELECT * FROM phpbb_posts WHERE forum_id = @forumId AND post_id <> @postId ORDER BY post_time DESC",
                    new { curForum.ForumId, deleted.PostId }
                );
                var lastForumPostUser = await _userService.GetAuthenticatedUserById(lastForumPost.PosterId);

                await SetForumLastPost(curForum, lastForumPost, lastForumPostUser, true);
            }

            await conn.ExecuteAsync(
                "UPDATE phpbb_users SET user_posts = user_posts - 1 WHERE user_id = @posterId",
                new { deleted.PosterId }
            );
        }

        private async Task SetTopicLastPost(PhpbbTopics topic, PhpbbPosts post, AuthenticatedUser author, bool hardReset = false)
        {
            if (hardReset || topic.TopicLastPostTime < post.PostTime)
            {
                topic.TopicLastPostId = post.PostId;
                topic.TopicLastPostSubject = post.PostSubject;
                topic.TopicLastPostTime = post.PostTime;
                topic.TopicLastPosterColour = author.UserColor;
                topic.TopicLastPosterId = post.PosterId;
                topic.TopicLastPosterName = author.UserId == Constants.ANONYMOUS_USER_ID ? post.PostUsername : author.Username;

                var conn = await _context.GetDbConnectionAsync();
                await conn.ExecuteAsync(
                    @"UPDATE phpbb_topics 
                         SET topic_last_post_id = @TopicLastPostId, 
                             topic_last_post_subject = @TopicLastPostSubject, 
                             topic_last_post_time = @TopicLastPostTime, 
                             topic_last_poster_colour = @TopicLastPosterColour, 
                             topic_last_poster_id = @TopicLastPosterId, 
                             topic_last_poster_name = @TopicLastPosterName 
                       WHERE topic_id = @TopicId",
                    topic
                );
            }
        }

        private async Task SetForumLastPost(PhpbbForums forum, PhpbbPosts post, AuthenticatedUser author, bool hardReset = false)
        {
            if (hardReset || forum.ForumLastPostTime < post.PostTime)
            {
                forum.ForumLastPostId = post.PostId;
                forum.ForumLastPostSubject = post.PostSubject;
                forum.ForumLastPostTime = post.PostTime;
                forum.ForumLastPosterColour = author.UserColor;
                forum.ForumLastPosterId = post.PosterId;
                forum.ForumLastPosterName = author.UserId == Constants.ANONYMOUS_USER_ID ? post.PostUsername : author.Username;

                var conn = await _context.GetDbConnectionAsync();
                
                await conn.ExecuteAsync(
                    @"UPDATE phpbb_forums 
                         SET forum_last_post_id = @ForumLastPostId, 
                             forum_last_post_subject = @ForumLastPostSubject, 
                             forum_last_post_time = @ForumLastPostTime, 
                             forum_last_poster_colour = @ForumLastPosterColour, 
                             forum_last_poster_id = @ForumLastPosterId, 
                             forum_last_poster_name = @ForumLastPosterName 
                       WHERE forum_id = @ForumId",
                    forum
                );
            }
        }

        private async Task SetTopicFirstPost(PhpbbTopics topic, PhpbbPosts post, AuthenticatedUser author, bool setTopicTitle, bool goForward = false)
        {
            var conn = await _context.GetDbConnectionAsync();

            var curFirstPost = await conn.QueryFirstOrDefaultAsync<PhpbbPosts>("SELECT * FROM phpbb_posts WHERE post_id = @TopicFirstPostId", new { topic.TopicFirstPostId });
            if (topic.TopicFirstPostId == 0 || goForward || (curFirstPost != null && curFirstPost.PostTime >= post.PostTime))
            {
                if (setTopicTitle)
                {
                    topic.TopicTitle = post.PostSubject.Replace(Constants.REPLY, string.Empty).Trim();
                }
                topic.TopicFirstPostId = post.PostId;
                topic.TopicFirstPosterColour = author.UserColor;
                topic.TopicFirstPosterName = author.Username;

                await conn.ExecuteAsync(
                    @"UPDATE phpbb_topics 
                         SET topic_title = @TopicTitle, 
                             topic_first_post_id = @TopicFirstPostId, 
                             topic_first_poster_colour = @TopicFirstPosterColour, 
                             topic_first_poster_name = @TopicFirstPosterName
                    WHERE topic_id = @topicId",
                    topic
                );
            }
        }
    }
}
