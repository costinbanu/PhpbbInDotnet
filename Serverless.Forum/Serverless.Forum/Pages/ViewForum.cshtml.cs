using Dapper;
using Microsoft.AspNetCore.Mvc;
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
        private bool _forceTreeRefresh;

        public HashSet<ForumTree> Forums { get; private set; }
        public List<TopicTransport> Topics { get; private set; }
        public string ForumTitle { get; private set; }
        public string ParentForumTitle { get; private set; }
        public int? ParentForumId { get; private set; }
        public bool IsNewPostView { get; private set; } = false;
        public bool IsOwnPostView { get; private set; } = false;
        public Paginator Paginator { get; private set; }

        [BindProperty(SupportsGet = true)]
        public int ForumId { get; set; }

        [BindProperty(SupportsGet = true)]
        public int? PageNum { get; set; }

        public ViewForumModel(ForumDbContext context, ForumTreeService forumService, UserService userService, CacheService cacheService)
            : base(context, forumService, userService, cacheService) { }

        public async Task<IActionResult> OnGet()
            => await WithValidForum(ForumId, async (thisForum) =>
            {
                ForumTitle = HttpUtility.HtmlDecode(thisForum?.ForumName ?? "untitled");
                //IEnumerable<dynamic> postCounts;
                IEnumerable<TopicDto> topics;

                using (var connection = _context.Database.GetDbConnection())
                {
                    await connection.OpenIfNeeded();
                    var parent = await connection.QuerySingleOrDefaultAsync<PhpbbForums>("SELECT * FROM phpbb_forums WHERE forum_id = @ParentId", new { thisForum.ParentId });
                    ParentForumId = parent?.ForumId;
                    ParentForumTitle = HttpUtility.HtmlDecode(parent?.ForumName ?? Constants.FORUM_NAME);
                    //postCounts = (
                    //    await connection.QueryAsync(
                    //        @"SELECT topic_id, count(*) AS count
                    //            FROM phpbb_posts
                    //           WHERE forum_id = @ForumId
                    //           GROUP BY topic_id",
                    //        new { ForumId }
                    //    )
                    //).Select(c => new { TopicId = (int)c.topic_id, Count = (int)c.count });
                    //topics = await connection.QueryAsync<PhpbbTopics>("SELECT * FROM phpbb_topics WHERE forum_id = @ForumId OR topic_type = @topicType ORDER BY topic_last_post_time DESC", new { ForumId, topicType = TopicType.Global });

                    topics = await connection.QueryAsync<TopicDto>(
                        @"SELECT t.topic_id, 
		                        t.forum_id,
		                        t.topic_title, 
		                        count(p.post_id) AS post_count,
		                        t.topic_views AS view_count,
		                        t.topic_type,
		                        t.topic_last_poster_id,
		                        t.topic_last_poster_name,
		                        t.topic_last_post_time,
		                        t.topic_last_poster_colour,
		                        t.topic_last_post_id
	                        FROM forum.phpbb_topics t
	                        JOIN forum.phpbb_posts p ON t.topic_id = p.topic_id
                        WHERE t.forum_id = 16 OR topic_type = 3
                        GROUP BY t.topic_id
                        ORDER BY topic_last_post_time DESC",
                        new { ForumId, topicType = TopicType.Global }
                    );
                }
                (Forums, _) = await GetForumTree(forceRefresh: _forceTreeRefresh, forumId: ForumId);
                var usr = await GetCurrentUserAsync();

                Topics = (
                    from t in topics

                    group t by t.TopicType into groups
                    orderby groups.Key descending
                    select new TopicTransport
                    {
                        TopicType = groups.Key,
                        Topics = groups
                    }
                ).ToList();

                return Page();
            });

        public async Task<IActionResult> OnGetNewPosts()
            => await WithRegisteredUser(async () =>
            {
                var usr = await GetCurrentUserAsync();
                var tree = await GetForumTree();
                IEnumerable<TopicDto> topics = null;
                using (var connection = _context.Database.GetDbConnection())
                {
                    await connection.OpenIfNeeded();
                    topics = await connection.QueryAsync<TopicDto>(
                        @"SELECT t.topic_id, 
	                            t.forum_id,
	                            t.topic_title, 
                                count(p.post_id) AS post_count,
                                t.topic_views AS view_count,
                                t.topic_last_poster_id,
                                t.topic_last_poster_name,
                                t.topic_last_post_time,
                                t.topic_last_poster_colour,
                                t.topic_last_post_id
                            FROM forum.phpbb_topics t
                            JOIN forum.phpbb_posts p ON t.topic_id = p.topic_id
                        WHERE t.topic_id IN @topicList
                        GROUP BY t.topic_id",
                        new { topicList = tree.Tracking.Select(t => t.TopicId).Distinct() }
                    );
                }

                //foreach (var track in tree.Tracking.Skip(((PageNum ?? 1) - 1) * Constants.DEFAULT_PAGE_SIZE).Take(Constants.DEFAULT_PAGE_SIZE))
                //{
                //    if (!tree.TopicData.TryGetValue(new PhpbbTopics { TopicId = track.TopicId }, out var topic))
                //    {
                //        continue;
                //    }
                //    var postCount = (int)(postCounts?.FirstOrDefault(p => p.topic_id == topic.TopicId)?.post_count ?? 0);
                //    var pageSize = usr.TopicPostsPerPage.ContainsKey(topic.TopicId) ? usr.TopicPostsPerPage[topic.TopicId] : Constants.DEFAULT_PAGE_SIZE;
                //    topics.Add(new TopicDto
                //    {
                //        TopicId = topic.TopicId,
                //        ForumId = topic.ForumId,
                //        TopicTitle = HttpUtility.HtmlDecode(topic.TopicTitle),
                //        TopicLastPosterId = topic.TopicLastPosterId == Constants.ANONYMOUS_USER_ID ? null as int? : topic.TopicLastPosterId,
                //        TopicLastPosterName = HttpUtility.HtmlDecode(topic.TopicLastPosterName),
                //        LastPostTime = topic.TopicLastPostTime.ToUtcTime(),
                //        PostCount = topic.TopicReplies,
                //        Pagination = new _PaginationPartialModel($"/ViewTopic?topicId={topic.TopicId}&pageNum=1", postCount, pageSize, 1, "PageNum"),
                //        Unread = true,
                //        TopicLastPosterColor = topic.TopicLastPosterColour,
                //        LastPostId = topic.TopicLastPostId,
                //        ViewCount = topic.TopicViews
                //    });
                //}

                Topics = new List<TopicTransport> { new TopicTransport { Topics = topics.OrderByDescending(t => t.LastPostTime) } };
                IsNewPostView = true;
                Paginator = new Paginator(tree.Tracking.Count, PageNum ?? 1, "/ViewForum?handler=NewPosts&pageNum=1");
                return Page();
            });

        public async Task<IActionResult> OnGetOwnPosts()
            => await WithRegisteredUser(async () =>
            {
                var usr = await GetCurrentUserAsync();
                var tree = await GetForumTree();
                IEnumerable<TopicDto> topics = null;
                var totalCount = 0;
                using (var connection = _context.Database.GetDbConnection())
                {
                    await connection.OpenIfNeeded();
                    using var multi = await connection.QueryMultipleAsync(
                        "CALL `forum`.`get_own_topics`(@userId, @skip, @take)",
                        new { usr.UserId, skip = ((PageNum ?? 1) - 1) * Constants.DEFAULT_PAGE_SIZE, take = Constants.DEFAULT_PAGE_SIZE }
                    );
                    topics = await multi.ReadAsync<TopicDto>();
                    totalCount = unchecked((int)await multi.ReadSingleAsync<long>());
                }

                //foreach (var ownTopic in ownTopics)
                //{
                //    if (!tree.TopicData.TryGetValue(new PhpbbTopics { TopicId = unchecked((int)ownTopic.topic_id) }, out var topic))
                //    {
                //        continue;
                //    }
                //    var pageSize = usr.TopicPostsPerPage.ContainsKey(topic.TopicId) ? usr.TopicPostsPerPage[topic.TopicId] : Constants.DEFAULT_PAGE_SIZE;
                //    topics.Add(new TopicDto
                //    {
                //        TopicId = topic.TopicId,
                //        ForumId = topic.ForumId,
                //        TopicTitle = HttpUtility.HtmlDecode(topic.TopicTitle),
                //        TopicLastPosterId = topic.TopicLastPosterId == Constants.ANONYMOUS_USER_ID ? null as int? : topic.TopicLastPosterId,
                //        TopicLastPosterName = HttpUtility.HtmlDecode(topic.TopicLastPosterName),
                //        LastPostTime = topic.TopicLastPostTime.ToUtcTime(),
                //        PostCount = topic.TopicReplies,
                //        Pagination = new _PaginationPartialModel($"/ViewTopic?topicId={topic.TopicId}&pageNum=1", (int)ownTopic.post_count, pageSize, 1, "PageNum"),
                //        Unread = IsTopicUnread(topic.TopicId).RunSync(),
                //        TopicLastPosterColor = topic.TopicLastPosterColour,
                //        LastPostId = topic.TopicLastPostId,
                //        ViewCount = topic.TopicViews
                //    });
                //}

                Topics = new List<TopicTransport> { new TopicTransport { Topics = topics.OrderByDescending(t => t.LastPostTime) } };
                IsOwnPostView = true;
                Paginator = new Paginator(totalCount, PageNum ?? 1, "/ViewForum?handler=OwnPosts&pageNum=1");
                return Page();
            });


        public async Task<IActionResult> OnPostMarkForumsRead()
            => await WithRegisteredUser(async () => await WithValidForum(ForumId, async (_) =>
            {
                var curForum = (await _forumService.GetForumTree(await GetCurrentUserAsync(), false, forumId: ForumId)).FirstOrDefault(f => f.ForumId == ForumId);
                foreach (var child in curForum.ChildrenList)
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