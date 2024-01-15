using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using PhpbbInDotnet.Database.SqlExecuter;
using PhpbbInDotnet.Forum.Models;
using PhpbbInDotnet.Languages;
using PhpbbInDotnet.Objects;
using PhpbbInDotnet.Services;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Web;

namespace PhpbbInDotnet.Forum.Pages
{
    [ValidateAntiForgeryToken, ResponseCache(NoStore = true, Duration = 0)]
    public class ViewForumModel : AuthenticatedPageModel
    {
        private bool _forceTreeRefresh;
        private readonly IBBCodeRenderingService _renderingService;
        private readonly INotificationService _notificationService;

        public HashSet<ForumTree>? Forums { get; private set; }
        public List<TopicGroup>? Topics { get; private set; }
        public string? ForumRulesLink { get; private set; }
        public string? ForumRules { get; private set; }
        public string? ForumRulesUid { get; private set; }
        public string? ForumDesc { get; private set; }
        public string? ForumTitle { get; private set; }
        public string? ParentForumTitle { get; private set; }
        public int? ParentForumId { get; private set; }
        public bool ShouldScrollToBottom { get; private set; }
        public bool IsSubscribed { get; private set; }
        public bool? SubscriptionToggleWasSuccessful { get; private set; }
        public string? SubscriptionToggleMessage { get; private set; }


        [BindProperty(SupportsGet = true)]
        public int ForumId { get; set; }

        public ViewForumModel(IForumTreeService forumService, IUserService userService, ISqlExecuter sqlExecuter, ITranslationProvider translationProvider,
            IConfiguration config, IBBCodeRenderingService renderingService, INotificationService notificationService)
            : base(forumService, userService, sqlExecuter, translationProvider, config)
        {
            _renderingService = renderingService;
            _notificationService = notificationService;
        }

        public async Task<IActionResult> OnGet()
            => await WithValidForum(ForumId, ForumId == 0, async (thisForum) =>
            {
                if (ForumId == 0)
                {
                    return RedirectToPage("Index");
                }

                var parentTask = SqlExecuter.QuerySingleOrDefaultAsync<(int ForumId, string ForumName)>(
                    "SELECT forum_id, forum_name FROM phpbb_forums WHERE forum_id = @ParentId",
                    new { thisForum.ParentId });
                var subscribedTask = _notificationService.IsSubscribedToForum(ForumUser.UserId, thisForum.ForumId);
                var topicGroupsTask = ForumService.GetTopicGroups(ForumId);
                await Task.WhenAll(parentTask, subscribedTask, topicGroupsTask);

                ForumTitle = HttpUtility.HtmlDecode(thisForum.ForumName ?? "untitled");
                ForumRulesLink = thisForum.ForumRulesLink;
                ForumRules = thisForum.ForumRules;
                ForumRulesUid = thisForum.ForumRulesUid;
                ForumDesc = _renderingService.BbCodeToHtml(thisForum.ForumDesc, thisForum.ForumDescUid ?? string.Empty);
                Topics = await topicGroupsTask;
                var parent = await parentTask;
                ParentForumId = parent.ForumId;
                ParentForumTitle = HttpUtility.HtmlDecode(string.IsNullOrWhiteSpace(parent.ForumName) ? Configuration.GetValue<string>("ForumName") : parent.ForumName);
                Forums = await ForumService.GetForumTree(ForumUser, _forceTreeRefresh, true);
                IsSubscribed = await subscribedTask;

                return Page();
            });

        public async Task<IActionResult> OnPostMarkForumsRead()
            => await WithRegisteredUser((_) => WithValidForum(ForumId, ForumId == 0, async (_) =>
            {
                await ForumService.MarkForumAndSubforumsRead(ForumUser, ForumId);
                _forceTreeRefresh = true;
                return await OnGet();
            }));

        public async Task<IActionResult> OnPostMarkTopicsRead()
            => await WithRegisteredUser((_) => WithValidForum(ForumId, async (_) =>
             {
                 await MarkShortcutsRead();
                 await ForumService.MarkForumRead(ForumUser.UserId, ForumId);

                 _forceTreeRefresh = true;

                 return await OnGet();
             }));

        public Task<IActionResult> OnPostToggleForumSubscription()
            => WithRegisteredUser(curUser => WithValidForum(ForumId, async curForum =>
            {
                (SubscriptionToggleMessage, SubscriptionToggleWasSuccessful) = await _notificationService.ToggleForumSubscription(curUser.UserId, curForum.ForumId);

                ShouldScrollToBottom = true;

                return await OnGet();
            }));

        async Task MarkShortcutsRead()
        {
            var shortcuts = await SqlExecuter.QueryAsync<(int actualForumId, int topicId, long topicLastPostTime)>(
                @"SELECT t.forum_id AS actual_forum_id, t.topic_id, t.topic_last_post_time
                    FROM phpbb_shortcuts s
                    JOIN phpbb_topics t on s.topic_id = t.topic_id
                   WHERE s.forum_id = @forumId",
                new { ForumId });

            foreach ((int actualForumId, int topicId, long topicLastPostTime) in shortcuts)
            {
                await ForumService.MarkTopicRead(ForumUser.UserId, actualForumId, topicId, isLastPage: true, topicLastPostTime);
            }
        }
    }
}