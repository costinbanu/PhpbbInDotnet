﻿using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PhpbbInDotnet.Database.Entities;
using PhpbbInDotnet.Database.SqlExecuter;
using PhpbbInDotnet.Forum.Models;
using PhpbbInDotnet.Languages;
using PhpbbInDotnet.Objects;
using PhpbbInDotnet.Services;
using Serilog;
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
        private readonly IBBCodeRenderingService _renderingService;
        private readonly ILogger _logger;

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

        public ViewForumModel(IForumTreeService forumService, IUserService userService, ISqlExecuter sqlExecuter,
            ITranslationProvider translationProvider, ILogger logger, IConfiguration config, IBBCodeRenderingService renderingService)
            : base(forumService, userService, sqlExecuter, translationProvider, config)
        {
            _renderingService = renderingService;
            _logger = logger;
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
                Topics = await ForumService.GetTopicGroups(ForumId);
                var parent = await SqlExecuter.QuerySingleOrDefaultAsync<(int ForumId, string ForumName)>(
                    "SELECT * FROM phpbb_forums WHERE forum_id = @ParentId",
                    new { thisForum.ParentId });
                ParentForumId = parent.ForumId;
                ParentForumTitle = HttpUtility.HtmlDecode(string.IsNullOrWhiteSpace(parent.ForumName) ? Configuration.GetValue<string>("ForumName") : parent.ForumName);
                Forums = await ForumService.GetForumTree(ForumUser, _forceTreeRefresh, true);

                return Page();
            });

        public IActionResult OnGetNewPosts()
        {
            _logger.Warning("Deprecated route requested for user '{user}' - ViewForum/{name}.", ForumUser.Username, nameof(OnGetNewPosts));
            return RedirectToPage("NewPosts");
        }

        public IActionResult OnGetOwnPosts()
        {
            _logger.Warning("Deprecated route requested for user '{user}' - ViewForum/{name}.", ForumUser.Username, nameof(OnGetOwnPosts));
            return RedirectToPage("OwnPosts");
        }

        public IActionResult OnGetDrafts()
        {
            _logger.Warning("Deprecated route requested for user '{user}' - ViewForum/{name}.", ForumUser.Username, nameof(OnGetDrafts));
            return RedirectToPage("Drafts");
        }

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