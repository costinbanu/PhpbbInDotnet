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
using System.Linq;
using System.Threading.Tasks;

namespace PhpbbInDotnet.Forum.Pages
{
    public class ModeratorModel : AuthenticatedPageModel
    {
        private readonly ModeratorService _moderatorService;

        [BindProperty(SupportsGet = true)]
        public ModeratorPanelMode Mode { get; set; }

        [BindProperty(SupportsGet = true)]
        public int ForumId { get; set; }

        [BindProperty(SupportsGet = true)]
        public int[] SelectedReports { get; set; }

        [BindProperty(SupportsGet = true)]
        public int[] SelectedTopics { get; set; }

        [BindProperty(SupportsGet = true)]
        public int? DestinationForumId { get; set; }

        [BindProperty(SupportsGet = true)]
        public string SelectedTopicIds { get; set; }

        [BindProperty(SupportsGet = true)]
        public ModeratorTopicActions? TopicAction { get; set; }

        public string ForumName { get; private set; }
        public List<TopicTransport> Topics { get; private set; }
        public List<ReportDto> Reports { get; private set; }
        public string MessageClass { get; private set; }
        public string Message { get; private set; }
        public bool ScrollToAction => TopicAction.HasValue && DestinationForumId.HasValue;

        public ModeratorModel(ForumDbContext context, ForumTreeService forumService, UserService userService, IAppCache cache, CommonUtils utils,
             IConfiguration config, AnonymousSessionCounter sessionCounter, LanguageProvider languageProvider, ModeratorService moderatorService)
            : base(context, forumService, userService, cache, config, sessionCounter, utils, languageProvider)
        {
            _moderatorService = moderatorService;
        }

        public int[] GetTopicIds()
        {
            if (SelectedTopics?.Any() ?? false)
            {
                return SelectedTopics;
            }
            return SelectedTopicIds?.Split(',')?.Select(x => int.TryParse(x, out var val) ? val : 0)?.Where(x => x != 0)?.ToArray() ?? Array.Empty<int>();
        }

        public async Task<IActionResult> OnGet()
            => await WithModerator(ForumId, async () =>
            {
                await Task.WhenAll(SetForumName(), SetTopics(), SetReports());
                return Page();

                async Task SetForumName()
                    => ForumName = (await Context.PhpbbForums.AsNoTracking().FirstOrDefaultAsync(f => f.ForumId == ForumId))?.ForumName;

                async Task SetTopics()
                {
                    var dbTopics = await (
                        from t in Context.PhpbbTopics.AsNoTracking()
                        where t.ForumId == ForumId
                        orderby t.TopicLastPostTime descending
                        select new TopicDto
                        {
                            TopicId = t.TopicId,
                            TopicTitle = t.TopicTitle,
                            TopicStatus = t.TopicStatus,
                            TopicType = t.TopicType,
                            TopicLastPosterColour = t.TopicLastPosterColour,
                            TopicLastPosterId = t.TopicLastPosterId,
                            TopicLastPosterName = t.TopicLastPosterName,
                            TopicLastPostId = t.TopicLastPostId,
                            TopicLastPostTime = t.TopicLastPostTime
                        }
                    ).ToListAsync();

                    Topics = (
                        from t in dbTopics
                        group t by t.TopicType into groups
                        orderby groups.Key descending
                        select new TopicTransport
                        {
                            TopicType = groups.Key,
                            Topics = groups
                        }
                    ).ToList();
                }
                async Task SetReports()
                    => Reports = await _moderatorService.GetReportedMessages(0);
            });

        public async Task<IActionResult> OnPostSubmitReports()
            => await WithModerator(ForumId, async () =>
            {
                if (!SelectedReports.Any())
                {
                    MessageClass = "message warning";
                    Message = LanguageProvider.Moderator[await GetLanguage(), "NO_REPORTS_SELECTED"];
                }
                else
                {
                    try
                    {
                        var connection = await Context.GetDbConnectionAsync();
                        await connection.ExecuteAsync(
                            "UPDATE phpbb_reports SET report_closed = 1 WHERE report_id IN @ids",
                            new { ids = SelectedReports ?? new[] { 0 } }
                        );
                        MessageClass = "message success";
                        Message = LanguageProvider.Moderator[await GetLanguage(), "REPORTS_CLOSED_SUCCESSFULLY"];
                    }
                    catch (Exception ex)
                    {
                        var id = Utils.HandleError(ex);
                        MessageClass = "message fail";
                        Message = string.Format(LanguageProvider.Errors[await GetLanguage(), "AN_ERROR_OCCURRED_TRY_AGAIN_ID_FORMAT"], id);
                    }
                }
                return await OnGet();
            });

        public async Task<IActionResult> OnPostSubmitTopics()
            => await WithModerator(ForumId, async () =>
            {
                var logDto = new OperationLogDto
                {
                    Action = TopicAction,
                    UserId = (await GetCurrentUserAsync()).UserId
                };

                try
                {
                    var results = await Task.WhenAll(
                        GetTopicIds().Select(
                            async topicId => TopicAction switch
                            {
                                ModeratorTopicActions.MakeTopicNormal => await _moderatorService.ChangeTopicType(topicId, TopicType.Normal, logDto),
                                ModeratorTopicActions.MakeTopicImportant => await _moderatorService.ChangeTopicType(topicId, TopicType.Important, logDto),
                                ModeratorTopicActions.MakeTopicAnnouncement => await _moderatorService.ChangeTopicType(topicId, TopicType.Announcement, logDto),
                                ModeratorTopicActions.MakeTopicGlobal => await _moderatorService.ChangeTopicType(topicId, TopicType.Global, logDto),
                                ModeratorTopicActions.MoveTopic => await _moderatorService.MoveTopic(topicId, DestinationForumId ?? 0, logDto),
                                ModeratorTopicActions.LockTopic => await _moderatorService.LockUnlockTopic(topicId, true, logDto),
                                ModeratorTopicActions.UnlockTopic => await _moderatorService.LockUnlockTopic(topicId, false, logDto),
                                _ => throw new NotSupportedException($"Can't perform action '{TopicAction}'")
                            }
                        )
                    );

                    if (results.All(r => r.IsSuccess ?? false))
                    {
                        MessageClass = "message success";
                        Message = string.Format(LanguageProvider.Errors[await GetLanguage(), "MODERATOR_TOPIC_ACTION_SUCCESSFUL_FORMAT"], TopicAction);
                    }
                    else if (results.All(r => !(r.IsSuccess ?? false)))
                    {
                        throw new AggregateException(results.Select(r => new Exception(r.Message)));
                    }
                    else
                    {
                        var failed = results.Where(r => !(r.IsSuccess ?? false));
                        var id = Utils.HandleErrorAsWarning(new AggregateException(failed.Select(r => new Exception(r.Message))));
                        MessageClass = "message warning";
                        Message = string.Format(LanguageProvider.Errors[await GetLanguage(), "MODERATOR_ACTION_PARTIAL_FAILED_FORMAT"], id);
                    }
                }
                catch (Exception ex)
                {
                    var id = Utils.HandleError(ex);
                    MessageClass = "message fail";
                    Message = string.Format(LanguageProvider.Errors[await GetLanguage(), "AN_ERROR_OCCURRED_TRY_AGAIN_ID_FORMAT"], id);
                }

                TopicAction = null;
                DestinationForumId = null;
                return await OnGet();
            });
    }
}
