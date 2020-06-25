using Dapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.EntityFrameworkCore;
using Serverless.Forum.Contracts;
using Serverless.Forum.ForumDb;
using Serverless.Forum.Pages.CustomPartials;
using Serverless.Forum.Services;
using Serverless.Forum.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;

namespace Serverless.Forum.Pages
{
    public class ViewForumModel : ModelWithLoggedUser
    {
        public List<ForumDto> Forums { get; private set; }
        public List<TopicTransport> Topics { get; private set; }
        public _PaginationPartialModel Pagination { get; private set; }
        public string ForumTitle { get; private set; }
        public string ParentForumTitle { get; private set; }
        public int? ParentForumId { get; private set; }
        public bool IsNewPostView { get; private set; } = false;

        [BindProperty(SupportsGet = true)]
        public int ForumId { get; set; }

        private bool _forceTreeRefresh = false;

        public ViewForumModel(ForumDbContext context, ForumTreeService forumService, UserService userService, CacheService cacheService)
            : base(context, forumService, userService, cacheService)
        {
        }

        public async Task<IActionResult> OnGet()
            => await WithValidForum(ForumId, async (thisForum) =>
            {
                ForumTitle = HttpUtility.HtmlDecode(thisForum?.ForumName ?? "untitled");
                IEnumerable<dynamic> postCounts;
                IEnumerable<PhpbbTopics> topics;

                using (var connection = _context.Database.GetDbConnection())
                {
                    await connection.OpenIfNeeded();
                    var parent = await connection.QuerySingleAsync<PhpbbForums>("SELECT * FROM phpbb_forums WHERE forum_id = @ParentId", new { thisForum.ParentId });
                    ParentForumId = parent.ForumId;
                    ParentForumTitle = HttpUtility.HtmlDecode(parent?.ForumName ?? "untitled");
                    postCounts = (
                        await connection.QueryAsync(
                            @"SELECT topic_id, count(*) AS count
                                FROM phpbb_posts
                               WHERE forum_id = @ForumId
                               GROUP BY topic_id",
                            new { ForumId }
                        )
                    ).Select(c => new { TopicId = (int)c.topic_id, Count = (int)c.count });
                    topics = await connection.QueryAsync<PhpbbTopics>("SELECT * FROM phpbb_topics WHERE forum_id = @ForumId OR topic_type = @topicType ORDER BY topic_last_post_time DESC", new { ForumId, topicType = TopicType.Global });
                }

                Forums = (await GetForum(ForumId, _forceTreeRefresh)).ChildrenForums.ToList();
                var usr = await GetCurrentUserAsync();

                Topics = (
                    from t in topics

                    group t by t.TopicType into groups
                    orderby groups.Key descending
                    select new TopicTransport
                    {
                        TopicType = groups.Key,
                        Topics = from g in groups

                                 let counts = postCounts.FirstOrDefault(p => p.TopicId == g.TopicId)
                                 let postCount = counts == null ? 0 : counts.Count
                                 let pageSize = usr.TopicPostsPerPage.ContainsKey(g.TopicId) ? usr.TopicPostsPerPage[g.TopicId] : 14

                                 select new TopicDto
                                 {
                                     Id = g.TopicId,
                                     Title = HttpUtility.HtmlDecode(g.TopicTitle),
                                     LastPosterId = g.TopicLastPosterId == Constants.ANONYMOUS_USER_ID ? null as int? : g.TopicLastPosterId,
                                     LastPosterName = HttpUtility.HtmlDecode(g.TopicLastPosterName),
                                     LastPostTime = g.TopicLastPostTime.ToUtcTime(),
                                     PostCount = g.TopicReplies,
                                     Pagination = new _PaginationPartialModel($"/ViewTopic?topicId={g.TopicId}&pageNum=1", postCount, pageSize, 1, "PageNum"),
                                     Unread = IsTopicUnread(g.TopicId, false),
                                     LastPosterColor = g.TopicLastPosterColour,
                                     LastPostId = g.TopicLastPostId,
                                     ViewCount = g.TopicViews
                                 }
                    }
                ).ToList();

                return Page();
            });

        public async Task<IActionResult> OnGetNewPosts()
            => await WithRegisteredUser(async () =>
            {
                var unread = GetUnreadTopicsAndParentsLazy();
                var usr = await GetCurrentUserAsync();
                Topics = new List<TopicTransport>
                {
                    new TopicTransport
                    {
                        Topics = await (
                            from g in _context.PhpbbTopics.AsNoTracking()
                            join ut in unread.Select(t => t.TopicId)
                            on g.TopicId equals ut
                            let postCount = _context.PhpbbPosts.Count(p => p.TopicId == g.TopicId)
                            let pageSize = usr.TopicPostsPerPage.ContainsKey(g.TopicId) ? usr.TopicPostsPerPage[g.TopicId] : 14
                            select new TopicDto
                            {
                                Id = g.TopicId,
                                Title = HttpUtility.HtmlDecode(g.TopicTitle),
                                LastPosterId = g.TopicLastPosterId == Constants.ANONYMOUS_USER_ID ? null as int? : g.TopicLastPosterId,
                                LastPosterName = HttpUtility.HtmlDecode(g.TopicLastPosterName),
                                LastPostTime = g.TopicLastPostTime.ToUtcTime(),
                                PostCount = g.TopicReplies,
                                Pagination = new _PaginationPartialModel($"/ViewTopic?topicId={g.TopicId}&pageNum=1", postCount, pageSize, 1, "PageNum"),
                                Unread = true,
                                LastPosterColor = g.TopicLastPosterColour,
                                LastPostId = g.TopicLastPostId,
                                ViewCount = g.TopicViews
                            }
                        ).ToListAsync()
                    }
                };
                IsNewPostView = true;
                return Page();
            });

        public async Task<IActionResult> OnPostMarkForumsRead()
            => await WithRegisteredUser(async () => await WithValidForum(ForumId, async (_) =>
            {
                var curForum = await _forumService.GetForumTree(usr: await GetCurrentUserAsync(), fromParent: ForumId);
                var childForums = _forumService.GetPathInTree(curForum, f => f.ChildrenForums.Any() ? 0 : (f.Id ?? 0)).Where(f => f != 0);
                foreach (var child in childForums)
                {
                    await UpdateTracking(_context, child);
                }
                await _context.SaveChangesAsync();
                _forceTreeRefresh = true;
                return await OnGet();
            }));

        public async Task<IActionResult> OnPostMarkTopicsRead()
            => await WithRegisteredUser(async() => await WithValidForum(ForumId, async(_) =>
            {
                await UpdateTracking(_context, ForumId);
                _forceTreeRefresh = true;
                return await OnGet();
            }));

        private async Task UpdateTracking(ForumDbContext context, int forumId)
        {
            var usrId = (await GetCurrentUserAsync()).UserId;
            var topicTracksToRemove = await (
                from t in context.PhpbbTopics

                where t.ForumId == forumId

                join tt in context.PhpbbTopicsTrack
                on t.TopicId equals tt.TopicId
                into joinedTopicTracks

                from jtt in joinedTopicTracks.DefaultIfEmpty()
                where jtt.UserId == usrId
                select jtt
            ).ToListAsync();

            context.PhpbbTopicsTrack.RemoveRange(topicTracksToRemove);
            var forumTrackToRemove = await context.PhpbbForumsTrack.FirstOrDefaultAsync(ft => ft.ForumId == forumId && ft.UserId == usrId);
            if (forumTrackToRemove != null)
            {
                context.PhpbbForumsTrack.Remove(forumTrackToRemove);
            }
            await context.SaveChangesAsync();

            await context.PhpbbForumsTrack.AddAsync(new PhpbbForumsTrack
            {
                ForumId = forumId,
                UserId = usrId,
                MarkTime = DateTime.UtcNow.ToUnixTimestamp()
            });
            await _context.SaveChangesAsync();
        }
    }
}