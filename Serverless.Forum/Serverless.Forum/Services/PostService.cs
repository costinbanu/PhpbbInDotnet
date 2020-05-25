using Dapper;
using Microsoft.EntityFrameworkCore;
using Serverless.Forum.Contracts;
using Serverless.Forum.ForumDb;
using Serverless.Forum.Utilities;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Serverless.Forum.Services
{
    public class PostService
    {
        private readonly ForumDbContext _context;
        private readonly UserService _userService;
        private readonly Utils _utils;

        public PostService(ForumDbContext context, UserService userService, Utils utils)
        {
            _context = context;
            _userService = userService;
            _utils = utils;
        }

        public async Task<(List<PhpbbPosts> Posts, int Page, int Count)> GetPostPageAsync(int userId, int? topicId, int? page, int? postId)
        {
            using var connection = _context.Database.GetDbConnection();
            await connection.OpenAsync();
            DefaultTypeMap.MatchNamesWithUnderscores = true;

            using var multi = await connection.QueryMultipleAsync("CALL `forum`.`get_posts`(@userId, @topicId, @page, @postId);", new { userId, topicId, page, postId });
            var toReturn = (
                Posts: multi.Read<PhpbbPosts>().ToList(),
                Page: multi.Read<int>().Single(),
                Count: multi.Read<int>().Single()
            );

            _utils.RunParallel(async (localContext) =>
            {
                var attachments = await (
                    from a in localContext.PhpbbAttachments
                    join p in toReturn.Posts
                    on a.PostMsgId equals p.PostId
                    select a
                ).ToListAsync();
                attachments.ForEach(a => a.DownloadCount++);
                localContext.PhpbbAttachments.UpdateRange(attachments);
                await localContext.SaveChangesAsync();
            });

            return toReturn;
        }

        public async Task<PollDisplay> GetPoll(PhpbbTopics _currentTopic)
        {
            var toReturn = new PollDisplay
            {
                PollTitle = _currentTopic.PollTitle,
                PollStart = _currentTopic.PollStart.ToUtcTime(),
                PollDurationSecons = _currentTopic.PollLength,
                PollMaxOptions = _currentTopic.PollMaxOptions,
                TopicId = _currentTopic.TopicId,
                VoteCanBeChanged = _currentTopic.PollVoteChange == 1,
                PollOptions = await (
                    from o in _context.PhpbbPollOptions.AsNoTracking()
                    where o.TopicId == _currentTopic.TopicId
                    let voters = from v in _context.PhpbbPollVotes.AsNoTracking()
                                 where o.PollOptionId == v.PollOptionId && o.TopicId == v.TopicId
                                 join u in _context.PhpbbUsers.AsNoTracking()
                                 on v.VoteUserId equals u.UserId
                                 into joinedUsers

                                 from ju in joinedUsers.DefaultIfEmpty()
                                 where ju != null
                                 select new PollOptionVoter { UserId = ju.UserId, Username = ju.Username }

                    select new PollOption
                    {
                        PollOptionId = o.PollOptionId,
                        PollOptionText = o.PollOptionText,
                        TopicId = o.TopicId,
                        PollOptionVoters = voters.ToList()
                    }
                ).ToListAsync()
            };

            if (!toReturn.PollOptions.Any())
            {
                toReturn = null;
            }

            return toReturn;
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
                await SetTopicLastPost(curTopic, added, usr);
            }

            if (curForum.ForumLastPostId == added.PostId)
            {
                await SetForumLastPost(curForum, added, usr);
            }
        }

        public async Task CascadePostAdd(ForumDbContext context, PhpbbPosts added, bool isFirstPost, bool ignoreTopic)
        {
            var curTopic = await context.PhpbbTopics.FirstOrDefaultAsync(t => t.TopicId == added.TopicId);
            var curForum = await context.PhpbbForums.FirstOrDefaultAsync(f => f.ForumId == curTopic.ForumId);
            var usr = await _userService.GetLoggedUserById(added.PosterId);

            await SetForumLastPost(curForum, added, usr);

            if (!ignoreTopic)
            {
                await SetTopicLastPost(curTopic, added, usr);
                if (isFirstPost)
                {
                    await SetTopicFirstPost(curTopic, added, usr, false);
                }
            }
        }

        public async Task CascadePostDelete(ForumDbContext context, PhpbbPosts deleted, bool ignoreTopic, int? oldTopicId = null)
        {
            oldTopicId ??= deleted.TopicId;
            var curTopic = await context.PhpbbTopics.FirstOrDefaultAsync(t => t.TopicId == oldTopicId);
            var curForum = await context.PhpbbForums.FirstOrDefaultAsync(f => f.ForumId == curTopic.ForumId);

            if (context.PhpbbPosts.Count(p => p.TopicId == oldTopicId.Value) == 0 && !ignoreTopic)
            {
                this never happens (if is always false?? why?)
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
                        group p by p.PostTime into grouped
                        orderby grouped.Key descending
                        select grouped.FirstOrDefault()
                    ).FirstOrDefaultAsync();
                    var lastTopicPostUser = await _userService.GetLoggedUserById(lastTopicPost.PosterId);

                    await SetTopicLastPost(curTopic, lastTopicPost, lastTopicPostUser);
                }

                if (curForum.ForumLastPostId == deleted.PostId)
                {
                    var lastForumPost = await (
                        from p in context.PhpbbPosts.AsNoTracking()
                        where p.ForumId == curForum.ForumId
                           && p.PostId != deleted.PostId
                        group p by p.PostTime into grouped
                        orderby grouped.Key descending
                        select grouped.FirstOrDefault()
                    ).FirstOrDefaultAsync();
                    var lastForumPostUser = await _userService.GetLoggedUserById(lastForumPost.PosterId);

                    await SetForumLastPost(curForum, lastForumPost, lastForumPostUser);
                }

                if (curTopic.TopicFirstPostId == deleted.PostId && !ignoreTopic)
                {
                    var firstPost = await (
                        from p in context.PhpbbPosts.AsNoTracking()
                        where p.TopicId == oldTopicId
                           && p.PostId != deleted.PostId
                        group p by p.PostTime into grouped
                        orderby grouped.Key ascending
                        select grouped.FirstOrDefault()
                    ).FirstOrDefaultAsync();
                    var firstPostUser = await _userService.GetLoggedUserById(firstPost.PosterId);

                    await SetTopicFirstPost(curTopic, firstPost, firstPostUser, false);
                }
            }
        }

        private async Task SetTopicLastPost(PhpbbTopics topic, PhpbbPosts post, LoggedUser author)
        {
            if (topic.TopicLastPostTime < post.PostTime)
            {
                topic.TopicLastPostId = post.PostId;
                topic.TopicLastPostSubject = post.PostSubject;
                topic.TopicLastPostTime = post.PostTime;
                topic.TopicLastPosterColour = author.UserColor;
                topic.TopicLastPosterId = post.PosterId;
                topic.TopicLastPosterName = author == await _userService.GetAnonymousLoggedUserAsync() ? post.PostUsername : author.Username;
            }
        }

        private async Task SetForumLastPost(PhpbbForums forum, PhpbbPosts post, LoggedUser author)
        {
            if (forum.ForumLastPostTime < post.PostTime)
            {
                forum.ForumLastPostId = post.PostId;
                forum.ForumLastPostSubject = post.PostSubject;
                forum.ForumLastPostTime = post.PostTime;
                forum.ForumLastPosterColour = author.UserColor;
                forum.ForumLastPosterId = post.PosterId;
                forum.ForumLastPosterName = author == await _userService.GetAnonymousLoggedUserAsync() ? post.PostUsername : author.Username;
            }
        }

        private async Task SetTopicFirstPost(PhpbbTopics topic, PhpbbPosts post, LoggedUser author, bool setTopicTitle)
        {
            var curFirstPost = await _context.PhpbbPosts.AsNoTracking().FirstOrDefaultAsync(p => p.PostId == topic.TopicFirstPostId);
            if (curFirstPost != null && curFirstPost.PostTime >= post.PostTime)
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
