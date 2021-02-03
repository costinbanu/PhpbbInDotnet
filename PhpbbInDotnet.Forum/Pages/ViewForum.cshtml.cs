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

namespace PhpbbInDotnet.Forum.Pages
{
    public class ViewForumModel : AuthenticatedPageModel
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
        public ViewForumMode Mode { get; private set; }
        public Paginator Paginator { get; private set; }

        [BindProperty(SupportsGet = true)]
        public int ForumId { get; set; }

        [BindProperty(SupportsGet = true)]
        public int? PageNum { get; set; }

        [BindProperty]
        public int[] SelectedDrafts { get; set; }

        [BindProperty]
        public string[] SelectedNewPosts { get; set; }

        public ViewForumModel(ForumDbContext context, ForumTreeService forumService, UserService userService, IAppCache cache, IConfiguration config, 
            AnonymousSessionCounter sessionCounter, CommonUtils utils, LanguageProvider languageProvider)
            : base(context, forumService, userService, cache, config, sessionCounter, utils, languageProvider) 
        { }

        public async Task<IActionResult> OnGet()
            => await WithValidForum(ForumId, ForumId == 0, async (thisForum) =>
            {
                if (ForumId == 0)
                {
                    return RedirectToPage("Index");
                }

                ForumTitle = HttpUtility.HtmlDecode(thisForum?.ForumName ?? "untitled");
                IEnumerable<TopicDto> topics;

                var connection = Context.Database.GetDbConnection();
                
                var parent = await connection.QuerySingleOrDefaultAsync<PhpbbForums>("SELECT * FROM phpbb_forums WHERE forum_id = @ParentId", new { thisForum.ParentId });
                ParentForumId = parent?.ForumId;
                ParentForumTitle = HttpUtility.HtmlDecode(parent?.ForumName ?? Config.GetValue<string>("ForumName"));
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
		                    t.topic_last_post_id,
                            t.topic_status
	                    FROM forum.phpbb_topics t
	                    JOIN forum.phpbb_posts p ON t.topic_id = p.topic_id
                    WHERE t.forum_id = @forumId OR topic_type = @topicType
                    GROUP BY t.topic_id
                    ORDER BY topic_last_post_time DESC",
                    new { ForumId, topicType = TopicType.Global }
                );
                
                (Forums, _) = await GetForumTree(_forceTreeRefresh, true);

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
                Mode = ViewForumMode.Forum;

                return Page();
            });

        public async Task<IActionResult> OnGetNewPosts()
            => await WithRegisteredUser(async (user) =>
            {
                var tree = await GetForumTree(_forceTreeRefresh, true);
                IEnumerable<TopicDto> topics = null;
                var topicList = tree.Tracking.SelectMany(t => t.Value).Select(t => t.TopicId).Distinct();
                var restrictedForums = await ForumService.GetRestrictedForumList(await GetCurrentUserAsync());
                var connection = Context.Database.GetDbConnection();

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

                Topics = new List<TopicTransport> { new TopicTransport { Topics = topics } };
                Mode = ViewForumMode.NewPosts;
                Paginator = new Paginator(count: topicList.Count(), pageNum: PageNum ?? 1, link: "/ViewForum?handler=NewPosts&pageNum=1", topicId: null);
                return Page();
            });

        public async Task<IActionResult> OnGetOwnPosts()
            => await WithRegisteredUser(async (user) =>
            {
                var tree = await GetForumTree(false, true);
                IEnumerable<TopicDto> topics = null;
                var totalCount = 0;
                var restrictedForums = await ForumService.GetRestrictedForumList(await GetCurrentUserAsync());
                var connection = Context.Database.GetDbConnection();
                
                using var multi = await connection.QueryMultipleAsync(
                    "CALL `forum`.`get_own_topics`(@userId, @skip, @take, @restrictedForumList)",
                    new
                    {
                        user.UserId,
                        skip = ((PageNum ?? 1) - 1) * Constants.DEFAULT_PAGE_SIZE,
                        take = Constants.DEFAULT_PAGE_SIZE,
                        restrictedForumList = string.Join(',', restrictedForums.Select(f => f.forumId).DefaultIfEmpty())
                    }
                );
                topics = await multi.ReadAsync<TopicDto>();
                totalCount = unchecked((int)await multi.ReadSingleAsync<long>());

                Topics = new List<TopicTransport> { new TopicTransport { Topics = topics.OrderByDescending(t => t.LastPostTime) } };
                Mode = ViewForumMode.OwnPosts;
                Paginator = new Paginator(count: totalCount, pageNum: PageNum ?? 1, link: "/ViewForum?handler=OwnPosts&pageNum=1", topicId: null);
                return Page();
            });

        public async Task<IActionResult> OnGetDrafts()
            => await WithRegisteredUser(async (user) =>
            {
                IEnumerable<TopicDto> topics = null;
                var restrictedForums = await ForumService.GetRestrictedForumList(await GetCurrentUserAsync());
                var count = 0;
                var connection = Context.Database.GetDbConnection();
                
                using var multi = await connection.QueryMultipleAsync(
                    "CALL `forum`.`get_drafts`(@userId, @skip, @take, @restrictedForumList);",
                    new
                    {
                        user.UserId,
                        skip = ((PageNum ?? 1) - 1) * Constants.DEFAULT_PAGE_SIZE,
                        take = Constants.DEFAULT_PAGE_SIZE,
                        restrictedForumList = string.Join(',', restrictedForums.Select(f => f.forumId).DefaultIfEmpty())
                    }
                );
                topics = await multi.ReadAsync<TopicDto>();
                count = unchecked((int)await multi.ReadSingleAsync<long>());
                

                Topics = new List<TopicTransport> { new TopicTransport { Topics = topics } };
                Mode = ViewForumMode.Drafts;
                Paginator = new Paginator(count: count, pageNum: PageNum ?? 1, link: "/ViewForum?handler=drafts&pageNum=1", topicId: null);
                return Page();
            });

        public async Task<IActionResult> OnPostMarkForumsRead()
            => await WithRegisteredUser(async (_) => await WithValidForum(ForumId, ForumId == 0, async (_) =>
            {
                await MarkForumAndSubforumsRead(ForumId);
                _forceTreeRefresh = true;
                return await OnGet();
            }));

        public async Task<IActionResult> OnPostMarkTopicsRead()
            => await WithRegisteredUser(async (_) => await WithValidForum(ForumId, async (_) =>
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
                    var forumId = int.TryParse(values[0], out var val) ? val : 0;
                    var topicId = int.TryParse(values[1], out val) ? val : 0;
                    await MarkTopicRead(forumId, topicId, true, DateTime.UtcNow.ToUnixTimestamp());
                }
                _forceTreeRefresh = true;
                return await OnGetNewPosts();
            });

        public async Task<IActionResult> OnPostDeleteDrafts()
            => await WithRegisteredUser(async (_) =>
            {
                var connection = Context.Database.GetDbConnection();
                await connection.ExecuteAsync("DELETE FROM phpbb_drafts WHERE draft_id IN @ids", new { ids = SelectedDrafts?.DefaultIfEmpty() ?? new[] { 0 } });
                return await OnGetDrafts();
            });
    }
}