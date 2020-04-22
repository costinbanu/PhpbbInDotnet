using Dapper;
using Microsoft.EntityFrameworkCore;
using Serverless.Forum.Contracts;
using Serverless.Forum.ForumDb;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Serverless.Forum.Services
{
    public class PostService
    {
        private readonly ForumDbContext _context;
        private readonly UserService _userService;

        private delegate (int index, string match) FirstIndexOf(string haystack, string needle, int startIndex);
        private delegate (string result, int endIndex) Transform(string haystack, string needle, int startIndex);

        public PostService(ForumDbContext context, UserService userService)
        {
            _context = context;
            _userService = userService;
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

            var attachments = from a in _context.PhpbbAttachments

                              join p in toReturn.Posts
                              on a.PostMsgId equals p.PostId

                              select new PhpbbAttachments
                              {
                                  AttachComment = a.AttachComment,
                                  AttachId = a.AttachId,
                                  DownloadCount = a.DownloadCount + 1,
                                  Extension = a.Extension,
                                  Filesize = a.Filesize,
                                  Filetime = a.Filetime,
                                  InMessage = a.InMessage,
                                  IsOrphan = a.IsOrphan,
                                  Mimetype = a.Mimetype,
                                  PhysicalFilename = a.PhysicalFilename,
                                  PosterId = a.PosterId,
                                  PostMsgId = a.PostMsgId,
                                  RealFilename = a.RealFilename,
                                  Thumbnail = a.Thumbnail,
                                  TopicId = a.TopicId
                              };

            _context.PhpbbAttachments.UpdateRange(attachments);
            await _context.SaveChangesAsync();

            return toReturn;
        }

        public async Task CascadePostEdit(ForumDbContext context, PhpbbPosts added, LoggedUser usr)
        {
            var curTopic = await context.PhpbbTopics.FirstOrDefaultAsync(t => t.TopicId == added.TopicId);
            var curForum = await context.PhpbbForums.FirstOrDefaultAsync(f => f.ForumId == curTopic.ForumId);

            if (curTopic.TopicFirstPostId == added.PostId)
            {
                SetTopicFirstPost(curTopic, added, usr);
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

        public async Task CascadePostAdd(ForumDbContext context, PhpbbPosts added, LoggedUser usr, bool isFirstPost)
        {
            var curTopic = await context.PhpbbTopics.FirstOrDefaultAsync(t => t.TopicId == added.TopicId);
            var curForum = await context.PhpbbForums.FirstOrDefaultAsync(f => f.ForumId == curTopic.ForumId);

            await SetTopicLastPost(curTopic, added, usr);
            await SetForumLastPost(curForum, added, usr);

            if (isFirstPost)
            {
                SetTopicFirstPost(curTopic, added, usr);
            }
        }

        public async Task CascadePostDelete(ForumDbContext context, PhpbbPosts deleted)
        {
            var curTopic = await context.PhpbbTopics.FirstOrDefaultAsync(t => t.TopicId == deleted.TopicId);
            var curForum = await context.PhpbbForums.FirstOrDefaultAsync(f => f.ForumId == curTopic.ForumId);

            if (context.PhpbbPosts.Count(p => p.TopicId == deleted.TopicId) == 0)
            {
                context.PhpbbTopics.Remove(curTopic);
            }
            else if (curTopic.TopicLastPostId == deleted.PostId)
            {
                if (curTopic.TopicLastPostId == deleted.PostId)
                {
                    var lastPost = await (
                        from p in context.PhpbbPosts
                        where p.TopicId == curTopic.TopicId
                        group p by p.PostTime into grouped
                        orderby grouped.Key descending
                        select grouped.FirstOrDefault()
                    ).FirstOrDefaultAsync();
                    var lastPostUser = await _userService.DbUserToLoggedUserAsync(
                        await context.PhpbbUsers.FirstOrDefaultAsync(u => u.UserId == lastPost.PosterId)
                    );

                    await SetTopicLastPost(curTopic, lastPost, lastPostUser);
                }

                if (curForum.ForumLastPostId == deleted.PostId)
                {
                    var lastPost = await (
                        from p in context.PhpbbPosts
                        where p.ForumId == curForum.ForumId
                        group p by p.PostTime into grouped
                        orderby grouped.Key descending
                        select grouped.FirstOrDefault()
                    ).FirstOrDefaultAsync();
                    var lastPostUser = await _userService.DbUserToLoggedUserAsync(
                        await context.PhpbbUsers.FirstOrDefaultAsync(u => u.UserId == lastPost.PosterId)
                    );

                    await SetForumLastPost(curForum, lastPost, lastPostUser);
                }

                if (curTopic.TopicFirstPostId == deleted.PostId)
                {
                    var firstPost = await (
                        from p in context.PhpbbPosts
                        where p.TopicId == deleted.TopicId
                        group p by p.PostTime into grouped
                        orderby grouped.Key ascending
                        select grouped.FirstOrDefault()
                    ).FirstOrDefaultAsync();
                    var firstPostUser = await _userService.DbUserToLoggedUserAsync(
                        await context.PhpbbUsers.FirstOrDefaultAsync(u => u.UserId == firstPost.PosterId)
                    );

                    SetTopicFirstPost(curTopic, firstPost, firstPostUser);
                }
            }
        }

        private async Task SetTopicLastPost(PhpbbTopics topic, PhpbbPosts post, LoggedUser author)
        {
            topic.TopicLastPostId = post.PosterId;
            topic.TopicLastPostSubject = post.PostSubject;
            topic.TopicLastPostTime = post.PostTime;
            topic.TopicLastPosterColour = author.UserColor;
            topic.TopicLastPosterId = post.PosterId;
            topic.TopicLastPosterName = author == await _userService.GetAnonymousLoggedUserAsync() ? post.PostUsername : author.Username;
        }

        private async Task SetForumLastPost(PhpbbForums forum, PhpbbPosts post, LoggedUser author)
        {
            forum.ForumLastPostId = post.PosterId;
            forum.ForumLastPostSubject = post.PostSubject;
            forum.ForumLastPostTime = post.PostTime;
            forum.ForumLastPosterColour = author.UserColor;
            forum.ForumLastPosterId = post.PosterId;
            forum.ForumLastPosterName = author == await _userService.GetAnonymousLoggedUserAsync() ? post.PostUsername : author.Username;
        }

        private void SetTopicFirstPost(PhpbbTopics topic, PhpbbPosts post, LoggedUser author)
        {
            topic.TopicFirstPostId = post.PostId;
            topic.TopicFirstPosterColour = author.UserColor;
            topic.TopicFirstPosterName = author.Username;
        }
    }
}
