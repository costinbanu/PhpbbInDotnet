using Dapper;
using LazyCache;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using PhpbbInDotnet.Database;
using PhpbbInDotnet.Database.Entities;
using PhpbbInDotnet.Objects;
using PhpbbInDotnet.Objects.Configuration;
using PhpbbInDotnet.Domain;
using PhpbbInDotnet.Domain.Extensions;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;

namespace PhpbbInDotnet.Services
{
    class PostService : IPostService
    {
        private readonly IForumDbContext _context;
        private readonly IUserService _userService;
        private readonly IAppCache _cache;
        private readonly ICommonUtils _utils;
        private readonly int _maxAttachmentCount;

        public PostService(IForumDbContext context, IUserService userService, IAppCache cache, ICommonUtils utils, IConfiguration config)
        {
            _context = context;
            _userService = userService;
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
                    var sqlExecuter = _context.GetSqlExecuter();
                    await sqlExecuter.ExecuteAsync(
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

        public async Task<PostListDto> GetPosts(int topicId, int pageNum, int pageSize, bool isPostingView, string language)
        {
            var posts = await _context.GetSqlExecuter().QueryAsync<PostDto>(
                     @"WITH ranks AS (
					    SELECT DISTINCT u.user_id, 
						       COALESCE(r1.rank_id, r2.rank_id) AS rank_id, 
						       COALESCE(r1.rank_title, r2.rank_title) AS rank_title
					      FROM phpbb_users u
					      JOIN phpbb_groups g ON u.group_id = g.group_id
					      LEFT JOIN phpbb_ranks r1 ON u.user_rank = r1.rank_id
					      LEFT JOIN phpbb_ranks r2 ON g.group_rank = r2.rank_id
				    )
				    SELECT 
					       p.forum_id,
					       p.topic_id,
					       p.post_id,
					       p.post_subject,
					       p.post_text,
					       CASE WHEN p.poster_id = @ANONYMOUS_USER_ID
							    THEN p.post_username 
							    ELSE a.username
					       END AS author_name,
					       p.poster_id as author_id,
					       p.bbcode_uid,
					       p.post_time,
					       a.user_colour as author_color,
					       a.user_avatar as author_avatar,
					       p.post_edit_count,
					       p.post_edit_reason,
					       p.post_edit_time,
					       e.username as post_edit_user,
					       r.rank_title as author_rank,
					       p.poster_ip as ip
				      FROM phpbb_posts p
				      LEFT JOIN phpbb_users a ON p.poster_id = a.user_id
				      LEFT JOIN phpbb_users e ON p.post_edit_user = e.user_id
				      LEFT JOIN ranks r ON a.user_id = r.user_id
				      WHERE topic_id = @topicId 
				      ORDER BY 
                        CASE             
                            WHEN @order = 'ASC' THEN post_time 
                        END ASC, 
                        CASE 
                            WHEN @order = 'DESC' THEN post_time 
                        END DESC 
				      LIMIT @skip, @take;",
                     new
                     {
                         Constants.ANONYMOUS_USER_ID,
                         topicId,
                         skip = (pageNum - 1) * pageSize,
                         take = pageSize,
                         order = isPostingView ? "DESC" : "ASC"
                     });

            var currentPostIds = posts.Select(p => p.PostId).DefaultIfEmpty().ToList();

            var countTask = isPostingView 
                ? Task.FromResult<int?>(null) 
                : _context.GetSqlExecuter().ExecuteScalarAsync<int?>(
                    "SELECT COUNT(post_id) FROM phpbb_posts WHERE topic_id = @topicId",
                    new { topicId });
            var dbAttachmentsTask = _context.GetSqlExecuter().QueryAsync<PhpbbAttachments>(
                "SELECT * FROM phpbb_attachments WHERE post_msg_id IN @currentPostIds",
                new { currentPostIds });
            var reportsTask = isPostingView
                ? Task.FromResult(Enumerable.Empty<ReportDto>())
                : _context.GetSqlExecuter().QueryAsync<ReportDto>(
                    @"SELECT r.report_id AS id, 
		                     rr.reason_title, 
                             rr.reason_description, 
                             r.report_text AS details, 
                             r.user_id AS reporter_id, 
                             u.username AS reporter_username, 
                             r.post_id,
                             r.report_time,
                             r.report_closed
	                    FROM phpbb_reports r
                        JOIN phpbb_reports_reasons rr ON r.reason_id = rr.reason_id
                        JOIN phpbb_users u on r.user_id = u.user_id
	                   WHERE r.post_id IN @postIds;",
                    new { postIds = currentPostIds });

            await Task.WhenAll(countTask, dbAttachmentsTask, reportsTask);

            var (CorrelationId, Attachments) = await CacheAttachmentsAndPrepareForDisplay((await dbAttachmentsTask).AsList(), language, currentPostIds.Count, false);

            return new PostListDto
            {
                Posts = posts.AsList(),
                Attachments = Attachments,
                AttachmentDisplayCorrelationId = CorrelationId,
                PostCount = await countTask,
                Reports = (await reportsTask).AsList()
            };
        }

        public async Task<PollDto?> GetPoll(PhpbbTopics _currentTopic)
        {
            var options = Enumerable.Empty<PhpbbPollOptions>();
            var voters = Enumerable.Empty<PollOptionVoter>();

            var sqlExecuter = _context.GetSqlExecuter();

            options = await sqlExecuter.QueryAsync<PhpbbPollOptions>("SELECT * FROM phpbb_poll_options WHERE topic_id = @TopicId ORDER BY poll_option_id", new { _currentTopic.TopicId });
            if (options.Any())
            {
                voters = await sqlExecuter.QueryAsync<PollOptionVoter>(
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
            var sqlExecuter = _context.GetSqlExecuter();

            var curTopic = await sqlExecuter.QueryFirstOrDefaultAsync<PhpbbTopics>("SELECT * FROM phpbb_topics WHERE topic_id = @topicId", new { added.TopicId });
            var curForum = await sqlExecuter.QueryFirstOrDefaultAsync<PhpbbForums>("SELECT * FROM phpbb_forums WHERE forum_id = @forumId", new { curTopic.ForumId });
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
            var sqlExecuter = _context.GetSqlExecuter();

            var curTopic = await sqlExecuter.QueryFirstOrDefaultAsync<PhpbbTopics>("SELECT * FROM phpbb_topics WHERE topic_id = @topicId", new { added.TopicId });
            var curForum = await sqlExecuter.QueryFirstOrDefaultAsync<PhpbbForums>("SELECT * FROM phpbb_forums WHERE forum_id = @forumId", new { curTopic.ForumId });
            var usr = await _userService.GetAuthenticatedUserById(added.PosterId);

            await SetForumLastPost(curForum, added, usr);

            if (!ignoreTopic)
            {
                await SetTopicLastPost(curTopic, added, usr);
                await SetTopicFirstPost(curTopic, added, usr, false);
            }

            await sqlExecuter.ExecuteAsync(
                "UPDATE phpbb_topics SET topic_replies = topic_replies + 1, topic_replies_real = topic_replies_real + 1 WHERE topic_id = @topicId; " +
                "UPDATE phpbb_users SET user_posts = user_posts + 1 WHERE user_id = @userId",
                new { curTopic.TopicId, usr.UserId }
            );
        }

        public async Task CascadePostDelete(PhpbbPosts deleted, bool ignoreTopic, bool ignoreAttachmentsAndReports)
        {
            var sqlExecuter = _context.GetSqlExecuter();
            var curTopic = await _context.PhpbbTopics.AsNoTracking().FirstOrDefaultAsync(t => t.TopicId == deleted.TopicId);

            if (curTopic != null && await _context.PhpbbPosts.AsNoTracking().AnyAsync(p => p.TopicId == deleted.TopicId))
            {
                if (curTopic.TopicLastPostId == deleted.PostId && !ignoreTopic)
                {
                    var lastTopicPost = await (
                        from p in _context.PhpbbPosts.AsNoTracking()
                        where p.TopicId == curTopic.TopicId && p.PostId != deleted.PostId
                        orderby p.PostTime descending
                        select p
                    ).FirstOrDefaultAsync();
                    if (lastTopicPost != null)
                    {
                        var lastTopicPostUser = await _userService.GetAuthenticatedUserById(lastTopicPost.PosterId);
                        await SetTopicLastPost(curTopic, lastTopicPost, lastTopicPostUser, true);
                    }
                }

                if (curTopic.TopicFirstPostId == deleted.PostId && !ignoreTopic)
                {
                    var firstTopicPost = await (
                        from p in _context.PhpbbPosts.AsNoTracking()
                        where p.TopicId == curTopic.TopicId && p.PostId != deleted.PostId
                        orderby p.PostTime ascending
                        select p
                    ).FirstOrDefaultAsync();
                    if (firstTopicPost != null)
                    {
                        var firstPostUser = await _userService.GetAuthenticatedUserById(firstTopicPost.PosterId);
                        await SetTopicFirstPost(curTopic, firstTopicPost, firstPostUser, false, true);
                    }
                }

                if (!ignoreTopic)
                {
                    await sqlExecuter.ExecuteAsync(
                        "UPDATE phpbb_topics SET topic_replies = GREATEST(topic_replies - 1, 0), topic_replies_real = GREATEST(topic_replies_real - 1, 0) WHERE topic_id = @topicId",
                        new { curTopic.TopicId }
                    );
                }
            }
            else
            {
                await sqlExecuter.ExecuteAsync("DELETE FROM phpbb_topics WHERE topic_id = @topicId", new { deleted.TopicId });
            }

            if (!ignoreAttachmentsAndReports)
            {
                await sqlExecuter.ExecuteAsync(
                    "DELETE FROM phpbb_reports WHERE post_id = @postId; " +
                    "DELETE FROM phpbb_attachments WHERE post_msg_id = @postId",
                    new { deleted.PostId }
                );
            }

            if (curTopic != null)
            {
                var curForum = await sqlExecuter.QueryFirstOrDefaultAsync<PhpbbForums>("SELECT * FROM phpbb_forums WHERE forum_id = @forumId", new { forumId = curTopic?.ForumId ?? deleted.ForumId });
                if (curForum != null && curForum.ForumLastPostId == deleted.PostId)
                {
                    var lastForumPost = await (
                        from p in _context.PhpbbPosts.AsNoTracking()
                        where p.ForumId == curForum.ForumId && p.PostId != deleted.PostId
                        orderby p.PostTime descending
                        select p
                    ).FirstOrDefaultAsync();
                    if (lastForumPost != null)
                    {
                        var lastForumPostUser = await _userService.GetAuthenticatedUserById(lastForumPost.PosterId);
                        await SetForumLastPost(curForum, lastForumPost, lastForumPostUser, true);
                    }
                }
            }

            await sqlExecuter.ExecuteAsync(
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
                topic.TopicLastPosterColour = author.UserColor!;
                topic.TopicLastPosterId = post.PosterId;
                topic.TopicLastPosterName = author.UserId == Constants.ANONYMOUS_USER_ID ? post.PostUsername : author.Username!;

                var sqlExecuter = _context.GetSqlExecuter();
                await sqlExecuter.ExecuteAsync(
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
                forum.ForumLastPosterColour = author.UserColor!;
                forum.ForumLastPosterId = post.PosterId;
                forum.ForumLastPosterName = author.UserId == Constants.ANONYMOUS_USER_ID ? post.PostUsername : author.Username!;

                var sqlExecuter = _context.GetSqlExecuter();

                await sqlExecuter.ExecuteAsync(
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
            var sqlExecuter = _context.GetSqlExecuter();

            var curFirstPost = await sqlExecuter.QueryFirstOrDefaultAsync<PhpbbPosts>("SELECT * FROM phpbb_posts WHERE post_id = @TopicFirstPostId", new { topic.TopicFirstPostId });
            if (topic.TopicFirstPostId == 0 || goForward || (curFirstPost != null && curFirstPost.PostTime >= post.PostTime))
            {
                if (setTopicTitle)
                {
                    topic.TopicTitle = post.PostSubject.Replace(Constants.REPLY, string.Empty).Trim();
                }
                topic.TopicFirstPostId = post.PostId;
                topic.TopicFirstPosterColour = author.UserColor!;
                topic.TopicFirstPosterName = author.Username!;

                await sqlExecuter.ExecuteAsync(
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
