using Dapper;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using PhpbbInDotnet.Database.Entities;
using PhpbbInDotnet.Database.SqlExecuter;
using PhpbbInDotnet.Domain;
using PhpbbInDotnet.Domain.Extensions;
using PhpbbInDotnet.Domain.Utilities;
using PhpbbInDotnet.Forum.Models;
using PhpbbInDotnet.Languages;
using PhpbbInDotnet.Objects;
using PhpbbInDotnet.Services;
using Serilog;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Web;

namespace PhpbbInDotnet.Forum.Pages
{
    [ValidateAntiForgeryToken]
    public class ViewTopicModel : AuthenticatedPageModel
    {
        #region Model

        [BindProperty]
        public int? ForumId { get; set; }

        [BindProperty(SupportsGet = true)]
        public int? PostId { get; set; }

        [BindProperty(SupportsGet = true)]
        public int? TopicId { get; set; }

        [BindProperty(SupportsGet = true)]
        public int? PageNum { get; set; }

        [BindProperty(SupportsGet = true)]
        public int? DestinationForumId { get; set; }

        [BindProperty(SupportsGet = true)]
        public int? DestinationTopicId { get; set; }

        [BindProperty(SupportsGet = true)]
        public ModeratorTopicActions? TopicAction { get; set; }

        [BindProperty(SupportsGet = true)]
        public ModeratorPostActions? PostAction { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? SelectedPostIds { get; set; }

        [BindProperty]
        public int[]? PostIdsForModerator { get; set; }

        [BindProperty]
        public int? ClosestPostId { get; set; }

        public PollDto? Poll { get; private set; }
        public List<PostDto>? Posts { get; private set; }
        public string? TopicTitle { get; private set; }
        public string? ForumRulesLink { get; private set; }
        public string? ForumRules { get; private set; }
        public string? ForumRulesUid { get; private set; }
        public string? ForumTitle { get; private set; }
        public (string? Message, bool? IsSuccess) ModeratorActionResult { get; private set; }
        public Paginator? Paginator { get; private set; }
        public Guid CorrelationId { get; private set; }
        public bool ScrollToSubscriptionToggle { get; private set; }
        public bool IsSubscribed { get; private set; }
        public bool? SubscriptionToggleWasSuccessful { get; private set; }
        public string? SubscriptionToggleMessage { get; private set; }

        public bool ShowTopic => (TopicAction == ModeratorTopicActions.MoveTopic
            || TopicAction == ModeratorTopicActions.CreateShortcut)
            && ((ModelState[nameof(DestinationForumId)]?.Errors?.Any() ?? false)
                || DestinationForumId.HasValue);

        public bool ShowPostTopic => PostAction == ModeratorPostActions.MoveSelectedPosts
            && ((ModelState[nameof(DestinationTopicId)]?.Errors?.Any() ?? false)
                || (ModelState[nameof(PostIdsForModerator)]?.Errors?.Any() ?? false)
                || DestinationTopicId.HasValue);

        public bool ShowPostForum => PostAction == ModeratorPostActions.SplitSelectedPosts
            && ((ModelState[nameof(DestinationForumId)]?.Errors?.Any() ?? false)
                || (ModelState[nameof(PostIdsForModerator)]?.Errors?.Any() ?? false)
                || DestinationForumId.HasValue);

        public bool ScrollToModeratorPanel => ShowTopic || ShowPostForum || ShowPostTopic || !string.IsNullOrWhiteSpace(ModeratorActionResult.Message);

        public bool IsLocked => (_currentTopic?.TopicStatus ?? 0) == 1;

        public int PostCount => _currentTopic?.TopicReplies ?? 0;

        public int ViewCount => _currentTopic?.TopicViews ?? 0;

        public Dictionary<int, List<AttachmentDto>>? Attachments { get; private set; }

        public List<ReportDto>? Reports { get; private set; }

        #endregion model

        private PhpbbTopics? _currentTopic;
        private readonly IPostService _postService;
        private readonly IModeratorService _moderatorService;
        private readonly IWritingToolsService _writingToolsService;
        private readonly ILogger _logger;
        private readonly INotificationService _notificationService;

        public ViewTopicModel(IForumTreeService forumService, IUserService userService, ISqlExecuter sqlExecuter, ITranslationProvider translationProvider, IPostService postService,
            IModeratorService moderatorService, IWritingToolsService writingToolsService, IConfiguration configuration, ILogger logger, INotificationService notificationService)
            : base(forumService, userService, sqlExecuter, translationProvider, configuration)
        {
            _postService = postService;
            _moderatorService = moderatorService;
            _writingToolsService = writingToolsService;
            _logger = logger;
            _notificationService = notificationService;
        }

        public async Task<IActionResult> OnGetByPostId()
            => await WithValidPost(PostId ?? 0, async (curForum, curTopic, _) =>
            {
                var pageSize = ForumUser.GetPageSize(curTopic.TopicId);
                var idx = (await SqlExecuter.CallStoredProcedureAsync<int>("get_post_position_in_topic",
                    new { curTopic.TopicId, PostId })).Single();
                var computedPageNum = idx / pageSize + (idx % pageSize != 0 ? 1 : 0);
                await PopulateModel(curForum, curTopic, computedPageNum);
                return Page();
            });

        public Task<IActionResult> OnGet()
            => WithValidTopic(TopicId ?? 0, async (curForum, curTopic) =>
            {
                await ResiliencyUtility.RetryOnceAsync(
                    toDo: () => PopulateModel(curForum, curTopic),
                    evaluateSuccess: () => Posts!.Count > 0 && (PageNum ?? 1) == Paginator!.CurrentPage,
                    fix: () => PageNum = Paginator!.CurrentPage);
                return Page();
            });

        public async Task<IActionResult> OnPostPagination(int topicId, int userPostsPerPage, int? postId)
            => await WithRegisteredUser(async (user) =>
            {
                await SqlExecuter.CallStoredProcedureAsync("update_pagination_preference",
                     new { user.UserId, topicId, userPostsPerPage });

                if (postId.HasValue)
                {
                    return RedirectToPage("ViewTopic", "ByPostId", new { PostId });
                }
                else
                {
                    return RedirectToPage("ViewTopic", new { TopicId, pageNum = 1 });
                }
            });

        public async Task<IActionResult> OnPostVote(int topicId, int[] votes, string queryString)
            => await WithRegisteredUser(user => WithValidTopic(topicId, async (_, topic) =>
            {
                if (topic.TopicStatus == 1 && !await UserService.IsUserModeratorInForum(ForumUser, topic.ForumId))
                {
                    return Unauthorized();
                }

                var existingVotes = (await SqlExecuter.QueryAsync<PhpbbPollVotes>("SELECT * FROM phpbb_poll_votes WHERE topic_id = @topicId AND vote_user_id = @UserId", new { topicId, user.UserId })).AsList();
                if (existingVotes.Count > 0 && topic.PollVoteChange == 0)
                {
                    return PageWithError(nameof(Poll), TranslationProvider.Errors[Language, "CANT_CHANGE_VOTE"]);
                }

                var noLongerVoted = from prev in existingVotes
                                    join cur in votes
                                    on prev.PollOptionId equals cur
                                    into joined
                                    from j in joined.DefaultIfEmpty()
                                    where j == default
                                    select prev.PollOptionId;
                await SqlExecuter.ExecuteAsync(
                    "DELETE FROM phpbb_poll_votes WHERE topic_id = @topicId AND vote_user_id = @UserId AND poll_option_id IN @noLongerVoted",
                    new { topicId, user.UserId, noLongerVoted = noLongerVoted.DefaultIfEmpty() });
                await SqlExecuter.ExecuteAsync(
                     "UPDATE phpbb_poll_options SET poll_option_total = poll_option_total - 1 WHERE topic_id = @topicId AND poll_option_id = @vote",
                     noLongerVoted.Select(vote => new { topicId, vote }));

                var newVotes = from cur in votes
                               join prev in existingVotes
                               on cur equals prev.PollOptionId
                               into joined
                               from j in joined.DefaultIfEmpty()
                               where j == default
                               select cur;

                await SqlExecuter.ExecuteAsync(
                    "INSERT INTO phpbb_poll_votes (topic_id, poll_option_id, vote_user_id, vote_user_ip) VALUES (@topicId, @vote, @UserId, @usrIp)",
                    newVotes.Select(vote => new { topicId, vote, user.UserId, usrIp = HttpContext.GetIpAddress() ?? string.Empty }));
                await SqlExecuter.ExecuteAsync(
                    "UPDATE phpbb_poll_options SET poll_option_total = poll_option_total + 1 WHERE topic_id = @topicId AND poll_option_id = @vote",
                    newVotes.Select(vote => new { topicId, vote }));
                return Redirect($"./ViewTopic{HttpUtility.UrlDecode(queryString)}");
            }));

        public Task<IActionResult> OnPostToggleTopicSubscription(int lastPostId)
            => WithRegisteredUser(curUser => WithValidTopic(TopicId ?? 0, async(_, curTopic) =>
            {
                (SubscriptionToggleMessage, SubscriptionToggleWasSuccessful) = await _notificationService.ToggleTopicSubscription(curUser.UserId, curTopic.TopicId);

                PostId = lastPostId;
                ScrollToSubscriptionToggle = true;

                return await OnGetByPostId();
            }));


        #region Moderator handlers

        public async Task<IActionResult> OnPostTopicModerator()
            => await WithModerator(ForumId ?? 0, async () =>
            {
                var lang = Language;

                if (TopicAction == null)
                {
                    ModeratorActionResult = (TranslationProvider.BasicText[lang, "SELECT_AN_OPTION"], false);
                    return await OnGet();
                }

                var logDto = new OperationLogDto
                {
                    Action = TopicAction.Value,
                    UserId = ForumUser.UserId
                };

                ModeratorActionResult = TopicAction switch
                {
                    ModeratorTopicActions.MakeTopicNormal => await _moderatorService.ChangeTopicType(TopicId!.Value, TopicType.Normal, logDto),
                    ModeratorTopicActions.MakeTopicImportant => await _moderatorService.ChangeTopicType(TopicId!.Value, TopicType.Important, logDto),
                    ModeratorTopicActions.MakeTopicAnnouncement => await _moderatorService.ChangeTopicType(TopicId!.Value, TopicType.Announcement, logDto),
                    ModeratorTopicActions.MakeTopicGlobal => await _moderatorService.ChangeTopicType(TopicId!.Value, TopicType.Global, logDto),
                    ModeratorTopicActions.MoveTopic => await _moderatorService.MoveTopic(TopicId!.Value, DestinationForumId!.Value, logDto),
                    ModeratorTopicActions.LockTopic => await _moderatorService.LockUnlockTopic(TopicId!.Value, true, logDto),
                    ModeratorTopicActions.UnlockTopic => await _moderatorService.LockUnlockTopic(TopicId!.Value, false, logDto),
                    ModeratorTopicActions.DeleteTopic => await _moderatorService.DeleteTopic(TopicId!.Value, logDto),
                    ModeratorTopicActions.CreateShortcut => await _moderatorService.CreateShortcut(TopicId!.Value, DestinationForumId!.Value, logDto),
                    _ => throw new NotImplementedException($"Unknown action '{TopicAction}'")
                };

                if (TopicAction == ModeratorTopicActions.DeleteTopic)
                {
                    return RedirectToPage("ViewForum", new { ForumId });
                }
                else if ((TopicAction == ModeratorTopicActions.MoveTopic || TopicAction == ModeratorTopicActions.CreateShortcut) && ModeratorActionResult.IsSuccess == true)
                {
                    var destinations = await Task.WhenAll(
                        CompressionUtility.CompressAndEncode($"<a href=\"{ForumLinkUtility.GetRelativeUrlToForum(DestinationForumId ?? 0)}\">{TranslationProvider.BasicText[lang, "GO_TO_NEW_FORUM"]}</a>"),
                        CompressionUtility.CompressAndEncode($"<a href=\"{ForumLinkUtility.GetRelativeUrlToTopic(TopicId ?? 0, PageNum ?? 0)}\">{TranslationProvider.BasicText[lang, "GO_TO_LAST_TOPIC"]}</a>")
                    );
                    return RedirectToPage("Confirm", "DestinationConfirmation", new { destinations });
                }
                else
                {
                    TopicAction = null;
                    return await OnGet();
                }
            });

        public async Task<IActionResult> OnPostPostModerator()
            => await WithModerator(ForumId ?? 0, () => ModeratePosts());

        public async Task<IActionResult> OnPostDeleteMyMessage()
            => await WithRegisteredUser(async (user) =>
            {
                var lang = Language;
                if (await UserService.IsUserModeratorInForum(ForumUser, ForumId ?? 0))
                {
                    return await ModeratePosts();
                }

                var postIds = GetModeratorPostIds();
                if (postIds.Length != 1 || PostAction != ModeratorPostActions.DeleteSelectedPosts)
                {
                    return Unauthorized();
                }

                var toDelete = await SqlExecuter.QueryFirstOrDefaultAsync<PhpbbPosts>(
                    "SELECT * FROM phpbb_posts WHERE post_id = @postId",
                    new { postId = postIds[0] });

                if (toDelete == null)
                {
                    return NotFound();
                }

                var lastPost = await SqlExecuter.QueryFirstOrDefaultAsync<PhpbbPosts>(
                    "SELECT * FROM phpbb_posts WHERE topic_id = @topicId ORDER BY post_time DESC",
                    new { toDelete.TopicId });

                var errorMessage = "&#x274C;&nbsp;{0}";
                if (lastPost is not null && toDelete.PostTime < lastPost.PostTime)
                {
                    ModeratorActionResult = (string.Format(errorMessage, TranslationProvider.Errors[lang, "POST_NO_LONGER_LAST"]), false);
                    PostId = postIds[0];
                    return await OnGet();
                }

                if (!(toDelete.PosterId == user.UserId && (user.PostEditTime == 0 || DateTime.UtcNow.Subtract(toDelete.PostTime.ToUtcTime()).TotalMinutes <= user.PostEditTime)))
                {
                    ModeratorActionResult = (string.Format(errorMessage, TranslationProvider.Errors[lang, "EDIT_TIME_EXPIRED"]), false);
                    PostId = postIds[0];
                    return await OnGet();
                }

                var curTopic = await SqlExecuter.QueryFirstOrDefaultAsync<PhpbbTopics>(
                    "SELECT * FROM phpbb_topics WHERE topic_id = @topicId",
                    new { toDelete.TopicId });

                if (curTopic?.TopicStatus.ToBool() ?? false)
                {
                    ModeratorActionResult = (string.Format(errorMessage, TranslationProvider.Errors[lang, "CANT_DELETE_POST_TOPIC_CLOSED", Casing.FirstUpper]), false);
                    PostId = postIds[0];
                    return await OnGet();
                }

                return await ModeratePosts();
            });

        public async Task<IActionResult> OnPostReportMessage(int? reportPostId, short? reportReasonId, string reportDetails)
            => await WithRegisteredUser((user) => WithValidPost(reportPostId ?? 0, async (_, _, _) =>
            {
                await SqlExecuter.ExecuteAsync(
                    "INSERT INTO phpbb_reports (post_id, user_id, reason_id, report_text, report_time, report_closed) " +
                    "VALUES (@PostId, @UserId, @ReasonId, @ReportText, @ReportTime, 0)",
                    new
                    {
                        PostId = reportPostId!.Value,
                        user.UserId,
                        ReasonId = reportReasonId!.Value,
                        ReportText = await _writingToolsService.PrepareTextForSaving(reportDetails),
                        ReportTime = DateTime.UtcNow.ToUnixTimestamp()
                    }
                );

                PostId = reportPostId;
                return await OnGet();
            }));

        public async Task<IActionResult> OnPostManageReport(int? reportPostId, int? reportId, bool? redirectToEdit, bool? deletePost)
            => await WithModerator(ForumId ?? 0, async () =>
            {
                var logDto = new OperationLogDto
                {
                    Action = ModeratorPostActions.DeleteSelectedPosts,
                    UserId = ForumUser.UserId
                };
                var (_, nextRemaining) = await GetSelectedAndNextRemainingPostIds(reportPostId ?? 0);
                if (deletePost ?? false)
                {
                    ModeratorActionResult = await _moderatorService.DeletePosts(new[] { reportPostId!.Value }, logDto);
                    PostId = nextRemaining;
                }
                else
                {
                    PostId = reportPostId;
                }

                await SqlExecuter.ExecuteAsync(
                    "UPDATE phpbb_reports SET report_closed = 1 WHERE report_id = @reportId;",
                    new { reportId }
                );

                if (!(deletePost ?? false) && (redirectToEdit ?? false))
                {
                    var reportedPost = await SqlExecuter.QueryFirstOrDefaultAsync<PhpbbPosts>(
                        "SELECT * FROM phpbb_posts WHERE post_id = @reportPostId",
                        new { reportPostId });

                    if (reportedPost == null)
                    {
                        PostId = nextRemaining;
                        return await OnGet();
                    }
                    return RedirectToPage("Posting", "editPost", new { reportedPost.ForumId, reportedPost.TopicId, reportedPost.PostId });
                }
                else
                {
                    return await OnGet();
                }
            });

        public async Task<IActionResult> OnPostDuplicatePost(int postIdForDuplication)
            => await WithModerator(ForumId ?? 0, async () =>
            {
                var logDto = new OperationLogDto
                {
                    Action = ModeratorPostActions.DuplicateSelectedPost,
                    UserId = ForumUser.UserId
                };
                ModeratorActionResult = await _moderatorService.DuplicatePost(postIdForDuplication, logDto);
                PostId = postIdForDuplication;
                return await OnGetByPostId();
            });

        private async Task<IActionResult> ModeratePosts()
        {
            var lang = Language;

            if (PostAction == null)
            {
                ModeratorActionResult = (TranslationProvider.BasicText[lang, "SELECT_AN_OPTION"], false);
                return await OnGet();
            }

            var logDto = new OperationLogDto
            {
                Action = PostAction.Value,
                UserId = ForumUser.UserId
            };
            var postIds = GetModeratorPostIds();
            ModeratorActionResult = PostAction switch
            {
                ModeratorPostActions.DeleteSelectedPosts => await _moderatorService.DeletePosts(postIds, logDto),
                ModeratorPostActions.MoveSelectedPosts => await _moderatorService.MovePosts(postIds, DestinationTopicId, logDto),
                ModeratorPostActions.SplitSelectedPosts => await _moderatorService.SplitPosts(postIds, DestinationForumId, logDto),
                _ => throw new NotImplementedException($"Unknown action '{PostAction}'")
            };

            if ((ModeratorActionResult.IsSuccess ?? false) && PostAction == ModeratorPostActions.DeleteSelectedPosts && postIds.Length == 1 && ClosestPostId.HasValue)
            {
                PostId = ClosestPostId;
                return await OnGetByPostId();
            }

            if (ModeratorActionResult.IsSuccess ?? false)
            {
                int? LatestSelected, NextRemaining;
                if (ClosestPostId.HasValue && PostAction == ModeratorPostActions.DeleteSelectedPosts)
                {
                    (LatestSelected, NextRemaining) = (null, ClosestPostId);
                }
                else
                {
                    (LatestSelected, NextRemaining) = await GetSelectedAndNextRemainingPostIds(postIds);
                }
                var destinations = new List<string>();
                if (LatestSelected != null)
                {
                    destinations.Add(await CompressionUtility.CompressAndEncode($"<a href=\"{ForumLinkUtility.GetRelativeUrlToPost(LatestSelected ?? 0)}\">{TranslationProvider.BasicText[lang, "GO_TO_NEW_TOPIC"]}</a>"));
                };

                if (NextRemaining != null)
                {
                    destinations.Add(await CompressionUtility.CompressAndEncode($"<a href=\"{ForumLinkUtility.GetRelativeUrlToPost(NextRemaining ?? 0)}\">{TranslationProvider.BasicText[lang, "GO_TO_LAST_TOPIC"]}</a>"));
                }
                else
                {
                    destinations.Add(await CompressionUtility.CompressAndEncode($"<a href=\"{ForumLinkUtility.GetRelativeUrlToForum(ForumId ?? 0)}\">{TranslationProvider.BasicText[lang, "GO_TO_LAST_FORUM"]}</a>"));
                }

                return RedirectToPage("Confirm", "DestinationConfirmation", new { destinations });
            }

            PostAction = null;
            return await OnGet();
        }

        public int[] GetModeratorPostIds()
        {
            if (PostIdsForModerator?.Any() ?? false)
            {
                return PostIdsForModerator;
            }
            return SelectedPostIds?.Split(',')?
                .Select(x => int.TryParse(x, out var val) ? val : 0)?
                .Where(x => x != 0)?
                .ToArray() ?? Array.Empty<int>();
        }

        public bool FilterModeratorTopicActions(ModeratorTopicActions action)
        {
            var exclusionList = new List<ModeratorTopicActions>
            {
                ModeratorTopicActions.RestoreTopic,
                ModeratorTopicActions.RemoveShortcut
            };

            if (_currentTopic!.TopicStatus.ToBool())
            {
                exclusionList.Add(ModeratorTopicActions.LockTopic);
            }
            else
            {
                exclusionList.Add(ModeratorTopicActions.UnlockTopic);
            }

            exclusionList.Add(_currentTopic.TopicType switch
            {
                TopicType.Normal => ModeratorTopicActions.MakeTopicNormal,
                TopicType.Important => ModeratorTopicActions.MakeTopicImportant,
                TopicType.Announcement => ModeratorTopicActions.MakeTopicAnnouncement,
                TopicType.Global => ModeratorTopicActions.MakeTopicGlobal,
                _ => throw new ArgumentException($"Unknown topic type '{_currentTopic.TopicType}' for topic id {_currentTopic.TopicId}.")
            });
            return !exclusionList.Any(e => e == action);
        }

        #endregion Moderator handlers

        private async Task PopulateModel(PhpbbForums curForum, PhpbbTopics curTopic, int computedPageNum = 1)
        {
            _currentTopic = curTopic;
            TopicId = curTopic.TopicId;
            PageNum = Paginator.NormalizePageNumberLowerBound(PageNum ?? computedPageNum);
            ForumId = curForum.ForumId;
            ForumTitle = HttpUtility.HtmlDecode(curForum.ForumName);

            var postList = await _postService.GetPosts(TopicId.Value, PageNum!.Value, ForumUser.GetPageSize(TopicId.Value), isPostingView: false, Language);

            Posts = postList.Posts;
            Attachments = postList.Attachments;
            Reports = postList.Reports;
            CorrelationId = postList.AttachmentDisplayCorrelationId;
            Paginator = new Paginator(postList.PostCount!.Value, PageNum!.Value, $"/ViewTopic?TopicId={TopicId}&PageNum=1", TopicId, ForumUser);
            TopicTitle = HttpUtility.HtmlDecode(_currentTopic.TopicTitle ?? "untitled");
            ForumRulesLink = curForum.ForumRulesLink;
            ForumRules = curForum.ForumRules;
            ForumRulesUid = curForum.ForumRulesUid;

            var pollTask = _postService.GetPoll(_currentTopic);
            var subscribedTask = _notificationService.IsSubscribedToTopic(ForumUser.UserId, TopicId!.Value);
            await Task.WhenAll(pollTask, subscribedTask);

            Poll = await pollTask;
            IsSubscribed = await subscribedTask;

            if (ForumId > 0 && TopicId > 0 && await ForumService.IsTopicUnread(ForumId.Value, TopicId.Value, ForumUser))
            {
                await ForumService.MarkTopicRead(ForumUser.UserId, ForumId.Value, TopicId.Value, Paginator.IsLastPage, Posts?.DefaultIfEmpty().Max(p => p?.PostTime ?? 0L) ?? 0);
            }
            try
            {
                await SqlExecuter.ExecuteAsyncWithoutResiliency(
					"UPDATE phpbb_topics SET topic_views = topic_views + 1 WHERE topic_id = @topicId",
                    new { TopicId },
                    commandTimeout: 10);
            }
            catch (Exception ex)
            {
                _logger.Warning(ex, "Failed to increment topic views for topic {id} ({name}).", TopicId, TopicTitle);
            }
        }

        private async Task<(int? LatestSelected, int? NextRemaining)> GetSelectedAndNextRemainingPostIds(params int[] idsToInclude)
        {
            var latestSelectedPost = await SqlExecuter.QueryFirstOrDefaultAsync<PhpbbPosts>(
                "SELECT * FROM phpbb_posts WHERE post_id IN @ids ORDER BY post_time DESC",
                new { ids = idsToInclude.DefaultIfEmpty() }
            );

            var postIds = GetModeratorPostIds();
            var nextRemainingPost = await SqlExecuter.QueryFirstOrDefaultAsync<PhpbbPosts>(
                "SELECT * FROM phpbb_posts WHERE topic_id = @topicId AND post_id NOT IN @ids AND post_time >= @time ORDER BY post_time ASC",
                new { topicId = TopicId!.Value, ids = postIds.DefaultIfEmpty(), time = latestSelectedPost?.PostTime })

                ?? await SqlExecuter.QueryFirstOrDefaultAsync<PhpbbPosts>(
                "SELECT * FROM phpbb_posts WHERE topic_id = @topicId AND post_id NOT IN @ids ORDER BY post_time DESC",
                new { topicId = TopicId.Value, ids = postIds.DefaultIfEmpty() });

            return (latestSelectedPost?.PostId, nextRemainingPost?.PostId);
        }
    }
}