using Dapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using PhpbbInDotnet.Objects;
using PhpbbInDotnet.Database;
using PhpbbInDotnet.Database.Entities;
using PhpbbInDotnet.Services;
using PhpbbInDotnet.Utilities;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System;
using PhpbbInDotnet.Languages;
using LazyCache;
using System.Data;

namespace PhpbbInDotnet.Forum.Pages
{
    [ValidateAntiForgeryToken]
    public class ViewForumModel : AuthenticatedPageModel
    {
        private bool _forceTreeRefresh;
        private readonly IConfiguration _config;
        private readonly BBCodeRenderingService _renderingService;

        public HashSet<ForumTree>? Forums { get; private set; }
        public List<TopicGroup>? Topics { get; private set; }
        public string? ForumRulesLink { get; private set; }
        public string? ForumRules { get; private set; }
        public string? ForumRulesUid { get; private set; }
        public string? ForumDesc { get; private set; }
        public string? ForumTitle { get; private set; }
        public string? ParentForumTitle { get; private set; }
        public int? ParentForumId { get; private set; }
        public ViewForumMode Mode { get; private set; }
        public Paginator? Paginator { get; private set; }

        [BindProperty(SupportsGet = true)]
        public int ForumId { get; set; }

        [BindProperty(SupportsGet = true)]
        public int? PageNum { get; set; }

        [BindProperty]
        public int[]? SelectedDrafts { get; set; }

        [BindProperty]
        public string[]? SelectedNewPosts { get; set; }

        public ViewForumModel(ForumDbContext context, ForumTreeService forumService, UserService userService, IAppCache cache, BBCodeRenderingService renderingService,
            IConfiguration config, CommonUtils utils, LanguageProvider languageProvider)
            : base(context, forumService, userService, cache, utils, languageProvider) 
        {
            _config = config;
            _renderingService = renderingService;
        }

        public async Task<IActionResult> OnGet()
            => await WithValidForum(ForumId, ForumId == 0, async (thisForum) =>
            {
                if (ForumId == 0)
                {
                    return RedirectToPage("Index");
                }

                ForumTitle = HttpUtility.HtmlDecode(thisForum.ForumName ?? "untitled");
                ForumRulesLink = thisForum.ForumRulesLink;
                ForumRules = thisForum.ForumRules;
                ForumRulesUid = thisForum.ForumRulesUid;
                ForumDesc = _renderingService.BbCodeToHtml(thisForum.ForumDesc, thisForum.ForumDescUid ?? string.Empty);

                var connection = await Context.GetDbConnectionAsync();
                
                var parentTask = connection.QuerySingleOrDefaultAsync<PhpbbForums>(
                    "SELECT * FROM phpbb_forums WHERE forum_id = @ParentId", 
                    new { thisForum.ParentId }
                );
                var topicsTask = connection.QueryAsync<TopicDto>(
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
		                    t.topic_last_post_id,
                            t.topic_status
	                    FROM forum.phpbb_topics t
	                    JOIN forum.phpbb_posts p ON t.topic_id = p.topic_id
                    WHERE t.forum_id = @forumId OR topic_type = @topicType
                    GROUP BY t.topic_id
                    ORDER BY topic_last_post_time DESC",
                    new { ForumId, topicType = TopicType.Global }
                );
                var treeTask = GetForumTree(_forceTreeRefresh, true);

                await Task.WhenAll(parentTask, topicsTask, treeTask);

                Topics = (
                    from t in await topicsTask

                    group t by t.TopicType into groups
                    orderby groups.Key descending
                    select new TopicGroup
                    {
                        TopicType = groups.Key,
                        Topics = groups
                    }
                ).ToList();
                var parent = await parentTask;
                ParentForumId = parent?.ForumId;
                ParentForumTitle = HttpUtility.HtmlDecode(parent?.ForumName ?? _config.GetValue<string>("ForumName"));
                (Forums, _) = await treeTask;
                Mode = ViewForumMode.Forum;

                return Page();
            });

        public async Task<IActionResult> OnGetNewPosts()
            => await WithRegisteredUser(async (user) =>
            {
                var tree = await GetForumTree(_forceTreeRefresh, true);
                var topicList = tree.Tracking!.SelectMany(t => t.Value).Select(t => t.TopicId).Distinct();

                var topics = await Context.GetDbConnection().QueryAsync<TopicDto>(
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
                        restrictedForumList = await GetRestrictedForums()
                    }
                );

                Topics = new List<TopicGroup> { new TopicGroup { Topics = topics } };
                Mode = ViewForumMode.NewPosts;
                Paginator = new Paginator(count: topicList.Count(), pageNum: PageNum ?? 1, link: "/ViewForum?handler=NewPosts&pageNum=1", topicId: null);
                return Page();
            });

        public async Task<IActionResult> OnGetOwnPosts()
            => await WithRegisteredUser(async (user) =>
            {
                var tree = await GetForumTree(false, true);
                IEnumerable<TopicDto>? topics = null;
                var totalCount = 0;
                var restrictedForumsTask = GetRestrictedForums();
                var connectionTask = Context.GetDbConnectionAsync();
                
                var topicsTask = Context.GetDbConnection().QueryAsync<TopicDto>(
                    @"WITH own_topics AS (
			            SELECT DISTINCT p.topic_id
			              FROM phpbb_posts p
			              JOIN phpbb_topics t 
			                ON p.topic_id = t.topic_id
			             WHERE p.poster_id = @userId
                           AND t.forum_id NOT IN @restrictedForumList
                        ORDER BY t.topic_last_post_time DESC
                        LIMIT @skip, @take
                    )
                    SELECT t.topic_id, 
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
                      FROM phpbb_posts p
                      JOIN own_topics ot
                        ON p.topic_id = ot.topic_id
                      JOIN phpbb_topics t
                        ON t.topic_id = ot.topic_id
                     GROUP BY p.topic_id;",
                    new
                    {
                        user.UserId,
                        skip = ((PageNum ?? 1) - 1) * Constants.DEFAULT_PAGE_SIZE,
                        take = Constants.DEFAULT_PAGE_SIZE,
                        restrictedForumList = await restrictedForumsTask
                    }
                );
                var countTask = Context.GetDbConnection().ExecuteScalarAsync<int>(
                    @"SELECT COUNT(DISTINCT topic_id) AS total_count 
	                    FROM phpbb_posts 
	                   WHERE poster_id = @user_id
                         AND forum_id NOT IN @restrictedForumList;",
                    new
                    {
                        user.UserId,
                        restrictedForumList = await restrictedForumsTask
                    });

                await Task.WhenAll(topicsTask, countTask);

                topics = await topicsTask;
                totalCount = await countTask;

                Topics = new List<TopicGroup> { new TopicGroup { Topics = topics.OrderByDescending(t => t.LastPostTime) } };
                Mode = ViewForumMode.OwnPosts;
                Paginator = new Paginator(count: totalCount, pageNum: PageNum ?? 1, link: "/ViewForum?handler=OwnPosts&pageNum=1", topicId: null);
                return Page();
            });

        public async Task<IActionResult> OnGetDrafts()
            => await WithRegisteredUser(async (user) =>
            {
                var restrictedForumsTask = GetRestrictedForums();
                var count = 0;
                var connectionTask = Context.GetDbConnectionAsync();
                
                var draftsTask = Context.GetDbConnection().QueryAsync<TopicDto>(
                    @"SELECT d.draft_id,
				             d.topic_id, 
				             d.forum_id,
				             d.draft_subject as topic_title,
				             d.save_time as topic_last_post_time,
				             t.topic_last_post_id
			            FROM forum.phpbb_drafts d
			            LEFT JOIN forum.phpbb_topics t
			              ON d.topic_id = t.topic_id
		               WHERE d.forum_id NOT IN @restrictedForumList
                         AND d.user_id = @userId
                         AND d.forum_id <> 0
                         AND (t.topic_id IS NOT NULL OR d.topic_id = 0)
                       ORDER BY d.save_time DESC
                       LIMIT @skip, @take",
                    new
                    {
                        user.UserId,
                        skip = ((PageNum ?? 1) - 1) * Constants.DEFAULT_PAGE_SIZE,
                        take = Constants.DEFAULT_PAGE_SIZE,
                        restrictedForumList = await restrictedForumsTask
                    }
                );
                var countTask = Context.GetDbConnection().ExecuteScalarAsync<int>(
                    @"SELECT COUNT(*) as total_count
                        FROM forum.phpbb_drafts d
	                    LEFT JOIN forum.phpbb_topics t
	                      ON d.topic_id = t.topic_id
	                   WHERE d.forum_id NOT IN @restrictedForumList
                         AND d.user_id = @user_id
	                     AND d.forum_id <> 0
	                     AND (t.topic_id IS NOT NULL OR d.topic_id = 0);",
                    new
                    {
                        user.UserId,
                        restrictedForumList = await restrictedForumsTask
                    });

                await Task.WhenAll(draftsTask, countTask);

                count = await countTask;                

                Topics = new List<TopicGroup> { new TopicGroup { Topics = await draftsTask } };
                Mode = ViewForumMode.Drafts;
                Paginator = new Paginator(count: count, pageNum: PageNum ?? 1, link: "/ViewForum?handler=drafts&pageNum=1", topicId: null);
                return Page();
            });

        public async Task<IActionResult> OnPostMarkForumsRead()
            => await WithRegisteredUser((_) => WithValidForum(ForumId, ForumId == 0, async (_) =>
            {
                await MarkForumAndSubforumsRead(ForumId);
                _forceTreeRefresh = true;
                return await OnGet();
            }));

        public async Task<IActionResult> OnPostMarkTopicsRead()
            => await WithRegisteredUser((_) => WithValidForum(ForumId, async (_) =>
             {
                 await MarkForumRead(ForumId);
                 _forceTreeRefresh = true;
                 return await OnGet();
             }));

        public async Task<IActionResult> OnPostMarkNewPostsRead()
            => await WithRegisteredUser(async (_) => 
            {
                foreach (var post in SelectedNewPosts ?? Enumerable.Empty<string>())
                {
                    var values = post?.Split(';');
                    if ((values?.Length ?? 0) != 2)
                    {
                        continue;
                    }
                    var forumId = int.TryParse(values![0], out var val) ? val : 0;
                    var topicId = int.TryParse(values[1], out val) ? val : 0;
                    await MarkTopicRead(forumId, topicId, true, DateTime.UtcNow.ToUnixTimestamp());
                }
                _forceTreeRefresh = true;
                return await OnGetNewPosts();
            });

        public async Task<IActionResult> OnPostDeleteDrafts()
            => await WithRegisteredUser(async (_) =>
            {
                var connection = await Context.GetDbConnectionAsync();
                await connection.ExecuteAsync("DELETE FROM phpbb_drafts WHERE draft_id IN @ids", new { ids = SelectedDrafts?.DefaultIfEmpty() ?? new[] { 0 } });
                return await OnGetDrafts();
            });

        private async Task<IEnumerable<int>> GetRestrictedForums()
        {
            var restrictedForums = await ForumService.GetRestrictedForumList(GetCurrentUser());
            return restrictedForums.Select(f => f.forumId).DefaultIfEmpty();
        }
    }
}