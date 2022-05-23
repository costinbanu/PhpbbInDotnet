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

        [BindProperty(SupportsGet = true)]
        public int ForumId { get; set; }

        public ViewForumModel(IForumDbContext context, ForumTreeService forumService, UserService userService, IAppCache cache, BBCodeRenderingService renderingService,
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
                var topicsTask = ForumService.GetTopicGroups(ForumId);
                var treeTask = GetForumTree(_forceTreeRefresh, true);

                await Task.WhenAll(parentTask, topicsTask, treeTask);

                Topics = await topicsTask;
                var parent = await parentTask;
                ParentForumId = parent?.ForumId;
                ParentForumTitle = HttpUtility.HtmlDecode(parent?.ForumName ?? _config.GetValue<string>("ForumName"));
                (Forums, _) = await treeTask;

                return Page();
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
                 await Task.WhenAll(
                     MarkShortcutsRead(),
                     MarkForumRead(ForumId));

                 _forceTreeRefresh = true;

                 return await OnGet();
             }));

        async Task MarkShortcutsRead()
        {
            var shortcuts = Context.GetDbConnection().Query(
                @"SELECT t.forum_id AS actual_forum_id, t.topic_id, t.topic_last_post_time
                    FROM phpbb_shortcuts s
                    JOIN phpbb_topics t on s.topic_id = t.topic_id
                   WHERE s.forum_id = @forumId",
                new { ForumId });

            foreach (var shortcut in shortcuts)
            {
                await MarkTopicRead(
                    forumId: (int)shortcut.actual_forum_id, 
                    topicId: (int)shortcut.topic_id, 
                    isLastPage: true, 
                    markTime: (long)shortcut.topic_last_post_time);
            }
        }
    }
}