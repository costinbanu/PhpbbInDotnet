using Dapper;
using Microsoft.EntityFrameworkCore;
using PhpbbInDotnet.Objects;
using PhpbbInDotnet.Database;
using PhpbbInDotnet.Database.Entities;
using PhpbbInDotnet.Utilities;
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

        public PostService(ForumDbContext context, UserService userService)
        {
            _context = context;
            _userService = userService;
        }

        public async Task<(List<PhpbbPosts> Posts, int Page, int Count)> GetPostPageAsync(int userId, int? topicId, int? page, int? postId)
        {
            var connection = _context.Database.GetDbConnection();

            using var multi = await connection.QueryMultipleAsync("CALL `forum`.`get_posts`(@userId, @topicId, @page, @postId);", new { userId, topicId, page, postId });
            var toReturn = (
                Posts: (await multi.ReadAsync<PhpbbPosts>()).AsList(),
                Page: await multi.ReadSingleAsync<int>(),
                Count: unchecked((int)await  multi.ReadSingleAsync<long>())
            );

            return toReturn;
        }

        public async Task<PollDto> GetPoll(PhpbbTopics _currentTopic)
        {
            var options = Enumerable.Empty<PhpbbPollOptions>();
            var voters = Enumerable.Empty<PollOptionVoter>();

            var connection = _context.Database.GetDbConnection();
            
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
            var conn = _context.Database.GetDbConnection();
            
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
            var conn = _context.Database.GetDbConnection();
            
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

        public async Task CascadePostDelete(PhpbbPosts deleted, bool ignoreTopic, int? oldTopicId = null)
        {
            oldTopicId ??= deleted.TopicId;
            var conn = _context.Database.GetDbConnection();
            
            var curTopic = await conn.QueryFirstOrDefaultAsync<PhpbbTopics>("SELECT * FROM phpbb_topics WHERE topic_id = @topicId", new { deleted.TopicId });
            var curForum = await conn.QueryFirstOrDefaultAsync<PhpbbForums>("SELECT * FROM phpbb_forums WHERE forum_id = @forumId", new { curTopic.ForumId });

            if (await conn.ExecuteScalarAsync<long>("SELECT COUNT(1) FROM phpbb_posts WHERE topic_id = @oldTopicId", new { oldTopicId }) == 0L && !ignoreTopic)
            {
                await conn.ExecuteAsync("DELETE FROM phpbb_topics WHERE topic_id = @topicId", new { curTopic.TopicId });
            }
            else
            {
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
                        "SELECT * FROM phpbb_posts WHERE topic_id = @oldTopicId AND post_id <> @postId ORDER BY post_time ASC",
                        new { oldTopicId, deleted.PostId }
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

                    var report = await conn.QueryFirstOrDefaultAsync<PhpbbReports>("SELECT * FROM phpbb_reports WHERE post_id = @postId", new { deleted.PostId });
                    if (report != null)
                    {
                        await conn.ExecuteAsync(
                            "DELETE FROM phpbb_reports WHERE report_id = @reportId; UPDATE phpbb_topics SET topic_reported = 0 WHERE topic_id = @topicId",
                            new { report.ReportId, curTopic.TopicId }
                        );
                    }
                }
            }

            if (curForum.ForumLastPostId == deleted.PostId)
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

        private async Task SetTopicLastPost(PhpbbTopics topic, PhpbbPosts post, AuthenticatedUser author, bool goBack = false)
        {
            if (goBack || topic.TopicLastPostTime < post.PostTime)
            {
                topic.TopicLastPostId = post.PostId;
                topic.TopicLastPostSubject = post.PostSubject;
                topic.TopicLastPostTime = post.PostTime;
                topic.TopicLastPosterColour = author.UserColor;
                topic.TopicLastPosterId = post.PosterId;
                topic.TopicLastPosterName = author.UserId == Constants.ANONYMOUS_USER_ID ? post.PostUsername : author.Username;

                var conn = _context.Database.GetDbConnection();
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

        private async Task SetForumLastPost(PhpbbForums forum, PhpbbPosts post, AuthenticatedUser author, bool goBack = false)
        {
            if (goBack || forum.ForumLastPostTime < post.PostTime)
            {
                forum.ForumLastPostId = post.PostId;
                forum.ForumLastPostSubject = post.PostSubject;
                forum.ForumLastPostTime = post.PostTime;
                forum.ForumLastPosterColour = author.UserColor;
                forum.ForumLastPosterId = post.PosterId;
                forum.ForumLastPosterName = author.UserId == Constants.ANONYMOUS_USER_ID ? post.PostUsername : author.Username;

                var conn = _context.Database.GetDbConnection();
                
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
            var conn = _context.Database.GetDbConnection();

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
