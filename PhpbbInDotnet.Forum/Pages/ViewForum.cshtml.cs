using Dapper;
using LazyCache;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using PhpbbInDotnet.Database;
using PhpbbInDotnet.Database.Entities;
using PhpbbInDotnet.Languages;
using PhpbbInDotnet.Objects;
using PhpbbInDotnet.Services;
using PhpbbInDotnet.Utilities;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Web;

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
               
                var parentTask = Context.GetDbConnection().QuerySingleOrDefaultAsync<PhpbbForums>(
                    "SELECT * FROM phpbb_forums WHERE forum_id = @ParentId", 
                    new { thisForum.ParentId }
                );
                var topicsTask = GetTopics();
                var treeTask = GetForumTree(_forceTreeRefresh, true);

                await Task.WhenAll(parentTask, topicsTask, treeTask);

                Topics = await topicsTask;
                var parent = await parentTask;
                ParentForumId = parent?.ForumId;
                ParentForumTitle = HttpUtility.HtmlDecode(parent?.ForumName ?? _config.GetValue<string>("ForumName"));
                (Forums, _) = await treeTask;
                Mode = ViewForumMode.Forum;

                return Page();

                async Task<List<TopicGroup>> GetTopics()
                {
                    var topics = await Context.GetDbConnection().QueryAsync<TopicDto>(
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

                        UNION ALL

                          SELECT t.topic_id, 
		                         t.forum_id,
		                         t.topic_title, 
		                         count(p.post_id) AS post_count,
		                         t.topic_views AS view_count,
		                         @shortcutType AS topic_type,
		                         t.topic_last_poster_id,
		                         t.topic_last_poster_name,
		                         t.topic_last_post_time,
		                         t.topic_last_poster_colour,
		                         t.topic_last_post_id,
		                         t.topic_status
	                        FROM forum.phpbb_topics t
	                        JOIN forum.phpbb_shortcuts s ON t.topic_id = s.topic_id
                            JOIN forum.phpbb_posts p ON t.topic_id = p.topic_id
                           WHERE s.forum_id = @forumId
                           GROUP BY t.topic_id
                           
                           ORDER BY topic_last_post_time DESC",
                        new { ForumId, topicType = TopicType.Global, shortcutType = TopicType.Shortcut });

                    return (from t in topics

                            group t by t.TopicType into groups
                            orderby groups.Key descending

                            select new TopicGroup
                            {
                                TopicType = groups.Key,
                                Topics = groups
                            }).ToList();
                }
            });

        public IActionResult OnGetNewPosts()
        {
            Utils.HandleErrorAsWarning(new Exception($"Deprecated route requested for user '{GetCurrentUser().Username}' - ViewForum/{nameof(OnGetNewPosts)}."));
            return RedirectToPage("NewPosts");
        }

        public IActionResult OnGetOwnPosts()
        {
            Utils.HandleErrorAsWarning(new Exception($"Deprecated route requested for user '{GetCurrentUser().Username}' - ViewForum/{nameof(OnGetOwnPosts)}."));
            return RedirectToPage("OwnPosts");
        }

        public IActionResult OnGetDrafts()
        {
            Utils.HandleErrorAsWarning(new Exception($"Deprecated route requested for user '{GetCurrentUser().Username}' - ViewForum/{nameof(OnGetDrafts)}."));
            return RedirectToPage("Drafts");
        }

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
    }
}