using Dapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Serverless.Forum.Contracts;
using Serverless.Forum.ForumDb;
using Serverless.Forum.ForumDb.Entities;
using Serverless.Forum.Services;
using Serverless.Forum.Utilities;
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
        public string ForumRulesLink { get; private set; }
        public string ForumRules { get; private set; }
        public string ForumRulesUid { get; private set; }
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

        public ViewForumModel(ForumDbContext context, ForumTreeService forumService, UserService userService, CacheService cacheService, IConfiguration config)
            : base(context, forumService, userService, cacheService, config) { }

        public async Task<IActionResult> OnGet()
            => await WithValidForum(ForumId, async (thisForum) =>
            {
                ForumTitle = HttpUtility.HtmlDecode(thisForum?.ForumName ?? "untitled");
                IEnumerable<TopicDto> topics;

                using (var connection = _context.Database.GetDbConnection())
                {
                    await connection.OpenIfNeeded();
                    var parent = await connection.QuerySingleOrDefaultAsync<PhpbbForums>("SELECT * FROM phpbb_forums WHERE forum_id = @ParentId", new { thisForum.ParentId });
                    ParentForumId = parent?.ForumId;
                    ParentForumTitle = HttpUtility.HtmlDecode(parent?.ForumName ?? _config.GetValue<string>("ForumName"));
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
                        WHERE t.forum_id = @forumId OR topic_type = @topicType
                        GROUP BY t.topic_id
                        ORDER BY topic_last_post_time DESC",
                        new { ForumId, topicType = TopicType.Global }
                    );
                }
                (Forums, _) = await GetForumTree(_forceTreeRefresh);
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

                ForumRulesLink = thisForum.ForumRulesLink;
                ForumRules = thisForum.ForumRules;
                ForumRulesUid = thisForum.ForumRulesUid;

                return Page();
            });

        public async Task<IActionResult> OnGetNewPosts()
            => await WithRegisteredUser(async () =>
            {
                var usr = await GetCurrentUserAsync();
                var tree = await GetForumTree();
                IEnumerable<TopicDto> topics = null;
                var topicList = tree.Tracking.SelectMany(t => t.Value).Select(t => t.TopicId).Distinct();
                var restrictedForums = await _forumService.GetRestrictedForumList(await GetCurrentUserAsync());
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
                          AND t.forum_id NOT IN @restrictedForumList
                        GROUP BY t.topic_id
                        ORDER BY t.topic_last_post_time DESC
                        LIMIT @skip, @take",
                        new 
                        { 
                            topicList = topicList.DefaultIfEmpty(), 
                            skip = ((PageNum ?? 1) - 1) * Constants.DEFAULT_PAGE_SIZE, 
                            take = Constants.DEFAULT_PAGE_SIZE,
                            restrictedForumList = restrictedForums.Select(f => f.forumId).DefaultIfEmpty()
                        }
                    );
                }

                Topics = new List<TopicTransport> { new TopicTransport { Topics = topics } };
                IsNewPostView = true;
                Paginator = new Paginator(count: tree.Tracking.Count, pageNum: PageNum ?? 1, link: "/ViewForum?handler=NewPosts&pageNum=1", topicId: null);
                return Page();
            });

        public async Task<IActionResult> OnGetOwnPosts()
            => await WithRegisteredUser(async () =>
            {
                var usr = await GetCurrentUserAsync();
                var tree = await GetForumTree();
                IEnumerable<TopicDto> topics = null;
                var totalCount = 0;
                var restrictedForums = await _forumService.GetRestrictedForumList(await GetCurrentUserAsync());
                using (var connection = _context.Database.GetDbConnection())
                {
                    await connection.OpenIfNeeded();
                    using var multi = await connection.QueryMultipleAsync(
                        "CALL `forum`.`get_own_topics`(@userId, @skip, @take, @restrictedForumList)",
                        new 
                        { 
                            usr.UserId, 
                            skip = ((PageNum ?? 1) - 1) * Constants.DEFAULT_PAGE_SIZE, take = Constants.DEFAULT_PAGE_SIZE,
                            restrictedForumList = string.Join(',', restrictedForums.Select(f => f.forumId).DefaultIfEmpty())
                        }
                    );
                    topics = await multi.ReadAsync<TopicDto>();
                    totalCount = unchecked((int)await multi.ReadSingleAsync<long>());
                }

                Topics = new List<TopicTransport> { new TopicTransport { Topics = topics.OrderByDescending(t => t.LastPostTime) } };
                IsOwnPostView = true;
                Paginator = new Paginator(count: totalCount, pageNum: PageNum ?? 1, link: "/ViewForum?handler=OwnPosts&pageNum=1", topicId: null);
                return Page();
            });


        public async Task<IActionResult> OnPostMarkForumsRead()
            => await WithRegisteredUser(async () => await WithValidForum(ForumId, async (_)  =>
            {
                await MarkForumAndSubforumsRead(ForumId);
                _forceTreeRefresh = true;
                return await OnGet();
            }));

        public async Task<IActionResult> OnPostMarkTopicsRead()
            => await WithRegisteredUser(async() => await WithValidForum(ForumId, async(_) =>
            {
                await MarkForumRead(ForumId);
                _forceTreeRefresh = true;
                return await OnGet();
            }));
    }
}