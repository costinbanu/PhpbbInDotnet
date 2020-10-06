using Dapper;
using Microsoft.EntityFrameworkCore;
using PhpbbInDotnet.DTOs;
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
            using var connection = _context.Database.GetDbConnection();
            await connection.OpenIfNeeded();

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

            using (var connection = _context.Database.GetDbConnection())
            {
                await connection.OpenIfNeeded();
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

        public async Task CascadePostEdit(ForumDbContext context, PhpbbPosts added)
        {
            var curTopic = await context.PhpbbTopics.FirstOrDefaultAsync(t => t.TopicId == added.TopicId);
            var curForum = await context.PhpbbForums.FirstOrDefaultAsync(f => f.ForumId == curTopic.ForumId);
            var usr = await _userService.GetLoggedUserById(added.PosterId);

            if (curTopic.TopicFirstPostId == added.PostId)
            {
                await SetTopicFirstPost(curTopic, added, usr, true);
            }

            if (curTopic.TopicLastPostId == added.PostId)
            {
                SetTopicLastPost(curTopic, added, usr);
            }

            if (curForum.ForumLastPostId == added.PostId)
            {
                SetForumLastPost(curForum, added, usr);
            }
        }

        public async Task CascadePostAdd(ForumDbContext context, PhpbbPosts added, bool ignoreTopic)
        {
            var curTopic = await context.PhpbbTopics.FirstOrDefaultAsync(t => t.TopicId == added.TopicId);
            var curForum = await context.PhpbbForums.FirstOrDefaultAsync(f => f.ForumId == curTopic.ForumId);
            var usr = await _userService.GetLoggedUserById(added.PosterId);

            SetForumLastPost(curForum, added, usr);

            if (!ignoreTopic)
            {
                SetTopicLastPost(curTopic, added, usr);
                await SetTopicFirstPost(curTopic, added, usr, false);
            }
            curTopic.TopicReplies++;
            curTopic.TopicRepliesReal++;
        }

        public async Task CascadePostDelete(ForumDbContext context, PhpbbPosts deleted, bool ignoreTopic, int? oldTopicId = null)
        {
            oldTopicId ??= deleted.TopicId;
            var curTopic = await context.PhpbbTopics.FirstOrDefaultAsync(t => t.TopicId == oldTopicId);
            var curForum = await context.PhpbbForums.FirstOrDefaultAsync(f => f.ForumId == curTopic.ForumId);

            if (await context.PhpbbPosts.CountAsync(p => p.TopicId == oldTopicId.Value) == 0 && !ignoreTopic)
            {
                context.PhpbbTopics.Remove(curTopic);
            }
            else
            {
                if (curTopic.TopicLastPostId == deleted.PostId && !ignoreTopic)
                {
                    var lastTopicPost = await (
                        from p in context.PhpbbPosts.AsNoTracking()
                        where p.TopicId == curTopic.TopicId
                           && p.PostId != deleted.PostId
                        orderby p.PostTime descending
                        select p
                    ).FirstOrDefaultAsync();
                    var lastTopicPostUser = await _userService.GetLoggedUserById(lastTopicPost.PosterId);

                    SetTopicLastPost(curTopic, lastTopicPost, lastTopicPostUser, true);
                }

                if (curTopic.TopicFirstPostId == deleted.PostId && !ignoreTopic)
                {
                    var firstPost = await (
                        from p in context.PhpbbPosts.AsNoTracking()
                        where p.TopicId == oldTopicId
                           && p.PostId != deleted.PostId
                        orderby p.PostTime ascending
                        select p
                    ).FirstOrDefaultAsync();
                    var firstPostUser = await _userService.GetLoggedUserById(firstPost.PosterId);

                    await SetTopicFirstPost(curTopic, firstPost, firstPostUser, false, true);
                }

                curTopic.TopicReplies -= curTopic.TopicReplies == 0 ? 0 : 1;
                curTopic.TopicRepliesReal -= curTopic.TopicRepliesReal == 0 ? 0 : 1;

                var report = await context.PhpbbReports.FirstOrDefaultAsync(r => r.PostId == deleted.PostId);
                if (report != null)
                {
                    context.PhpbbReports.Remove(report);
                    curTopic.TopicReported = 0;
                }
            }

            if (curForum.ForumLastPostId == deleted.PostId)
            {
                var lastForumPost = await (
                    from p in context.PhpbbPosts.AsNoTracking()
                    where p.ForumId == curForum.ForumId
                       && p.PostId != deleted.PostId
                    orderby p.PostTime descending
                    select p
                ).FirstOrDefaultAsync();
                var lastForumPostUser = await _userService.GetLoggedUserById(lastForumPost.PosterId);

                SetForumLastPost(curForum, lastForumPost, lastForumPostUser, true);
            }
        }

        private void SetTopicLastPost(PhpbbTopics topic, PhpbbPosts post, LoggedUser author, bool goBack = false)
        {
            if (goBack || topic.TopicLastPostTime < post.PostTime)
            {
                topic.TopicLastPostId = post.PostId;
                topic.TopicLastPostSubject = post.PostSubject;
                topic.TopicLastPostTime = post.PostTime;
                topic.TopicLastPosterColour = author.UserColor;
                topic.TopicLastPosterId = post.PosterId;
                topic.TopicLastPosterName = author.UserId == Constants.ANONYMOUS_USER_ID ? post.PostUsername : author.Username;
            }
        }

        private void SetForumLastPost(PhpbbForums forum, PhpbbPosts post, LoggedUser author, bool goBack = false)
        {
            if (goBack || forum.ForumLastPostTime < post.PostTime)
            {
                forum.ForumLastPostId = post.PostId;
                forum.ForumLastPostSubject = post.PostSubject;
                forum.ForumLastPostTime = post.PostTime;
                forum.ForumLastPosterColour = author.UserColor;
                forum.ForumLastPosterId = post.PosterId;
                forum.ForumLastPosterName = author.UserId == Constants.ANONYMOUS_USER_ID ? post.PostUsername : author.Username;
            }
        }

        private async Task SetTopicFirstPost(PhpbbTopics topic, PhpbbPosts post, LoggedUser author, bool setTopicTitle, bool goForward = false)
        {
            var curFirstPost = await _context.PhpbbPosts.AsNoTracking().FirstOrDefaultAsync(p => p.PostId == topic.TopicFirstPostId);
            if (topic.TopicFirstPostId == 0 || goForward || (curFirstPost != null && curFirstPost.PostTime >= post.PostTime))
            {
                if (setTopicTitle)
                {
                    topic.TopicTitle = post.PostSubject.Replace(Constants.REPLY, string.Empty).Trim();
                }
                topic.TopicFirstPostId = post.PostId;
                topic.TopicFirstPosterColour = author.UserColor;
                topic.TopicFirstPosterName = author.Username;
            }
        }
    }
}
