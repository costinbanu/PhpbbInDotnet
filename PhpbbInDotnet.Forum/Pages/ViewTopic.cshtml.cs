using Dapper;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using PhpbbInDotnet.Objects;
using PhpbbInDotnet.Database;
using PhpbbInDotnet.Database.Entities;
using PhpbbInDotnet.Services;
using PhpbbInDotnet.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using PhpbbInDotnet.Languages;
using LazyCache;
using System.Data;

namespace PhpbbInDotnet.Forum.Pages
{
    [ValidateAntiForgeryToken]
    public class ViewTopicModel : AuthenticatedPageModel
    {
        [BindProperty]
        public int? ForumId { get; set; }

        [BindProperty(SupportsGet = true)]
        public int? PostId { get; set; }

        [BindProperty(SupportsGet = true)]
        public bool? Highlight { get; set; }

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
        public string SelectedPostIds { get; set; }

        [BindProperty]
        public int[] PostIdsForModerator { get; set; }

        [BindProperty]
        public int? ClosestPostId { get; set; }

        public PollDto Poll { get; private set; }
        public List<PostDto> Posts { get; private set; }
        public string TopicTitle { get; private set; }
        public string ForumRulesLink { get; private set; }
        public string ForumRules { get; private set; }
        public string ForumRulesUid { get; private set; }
        public string ForumTitle { get; private set; }
        public string ModeratorActionResult { get; private set; }
        public Paginator Paginator { get; private set; }
        public Guid CorrelationId { get; private set; }

        public bool ShowTopic => TopicAction == ModeratorTopicActions.MoveTopic && (
            (ModelState[nameof(DestinationForumId)]?.Errors?.Any() ?? false) ||
            DestinationForumId.HasValue
        );

        public bool ShowPostTopic => PostAction == ModeratorPostActions.MoveSelectedPosts && (
            (ModelState[nameof(DestinationTopicId)]?.Errors?.Any() ?? false) ||
            (ModelState[nameof(PostIdsForModerator)]?.Errors?.Any() ?? false) ||
            DestinationTopicId.HasValue
        );

        public bool ShowPostForum => PostAction == ModeratorPostActions.SplitSelectedPosts && (
            (ModelState[nameof(DestinationForumId)]?.Errors?.Any() ?? false) ||
            (ModelState[nameof(PostIdsForModerator)]?.Errors?.Any() ?? false) ||
            DestinationForumId.HasValue
        );

        public string ScrollToModeratorPanel => (ShowTopic || ShowPostForum || ShowPostTopic || !string.IsNullOrWhiteSpace(ModeratorActionResult)).ToString().ToLower();

        public bool IsLocked => (_currentTopic?.TopicStatus ?? 0) == 1;

        public int PostCount => _currentTopic?.TopicReplies ?? 0;

        public int ViewCount => _currentTopic?.TopicViews ?? 0;
        
        public Dictionary<int, List<AttachmentDto>> Attachments { get; private set; }
        
        public List<ReportDto> Reports { get; private set; }

        private PhpbbTopics _currentTopic;
        private PhpbbForums _currentForum;
        private int? _count;
        private readonly PostService _postService;
        private readonly ModeratorService _moderatorService;
        private readonly WritingToolsService _writingToolsService;

        public ViewTopicModel(ForumDbContext context, ForumTreeService forumService, UserService userService, IAppCache cache, CommonUtils utils, PostService postService, 
            ModeratorService moderatorService, WritingToolsService writingToolsService, LanguageProvider languageProvider)
            : base(context, forumService, userService, cache, utils, languageProvider)
        {
            _postService = postService;
            _moderatorService = moderatorService;
            _writingToolsService = writingToolsService;
        }

        public async Task<IActionResult> OnGetByPostId()
            => await WithValidPost(PostId ?? 0, async (curForum, curTopic, curPost) =>
            {
                _currentTopic = curTopic;
                _currentForum = curForum;
                await GetPostsLazy(null, null, PostId);

                TopicId = _currentTopic.TopicId;

                return await OnGet();
            });

        public async Task<IActionResult> OnGet()
        {
            if (_currentForum != null && _currentTopic != null)
            {
                return await toDo(_currentForum, _currentTopic);
            }
            else
            {
                return await WithValidTopic(TopicId ?? 0, (curForum, curTopic) => toDo(curForum, curTopic));
            }

            async Task<IActionResult> toDo(PhpbbForums curForum, PhpbbTopics curTopic)
            {
                _currentTopic = curTopic;

                if ((PageNum ?? 0) <= 0)
                {
                    PageNum = 1;
                }

                ForumId = curForum?.ForumId;
                ForumTitle = HttpUtility.HtmlDecode(curForum?.ForumName ?? "untitled");

                var postsTask = GetPostsLazy(TopicId, PageNum, null);

                Paginator = new Paginator(_count.Value, PageNum.Value, $"/ViewTopic?TopicId={TopicId}&PageNum=1", TopicId, GetCurrentUser());
                var pollTask = _postService.GetPoll(_currentTopic);

                TopicTitle = HttpUtility.HtmlDecode(_currentTopic.TopicTitle ?? "untitled");
                ForumRulesLink = curForum.ForumRulesLink;
                ForumRules = curForum.ForumRules;
                ForumRulesUid = curForum.ForumRulesUid;

                var markAsReadTask = MarkAsRead();
                var updateViewCountTask =  (await Context.GetDbConnectionAsync()).ExecuteAsync(
                    "UPDATE phpbb_topics SET topic_views = topic_views + 1 WHERE topic_id = @topicId",
                    new { topicId = TopicId.Value }
                );

                await Task.WhenAll(postsTask, pollTask, markAsReadTask, updateViewCountTask);

                Poll = await pollTask;

                return Page();
            }

            async Task MarkAsRead()
            {
                if (await IsTopicUnread(ForumId ?? 0, TopicId ?? 0))
                {
                    await MarkTopicRead(ForumId ?? 0, TopicId ?? 0, Paginator.IsLastPage, Posts.DefaultIfEmpty().Max(p => p?.PostTime ?? 0L));
                }
            }
        }

        public async Task<IActionResult> OnPostPagination(int topicId, int userPostsPerPage, int? postId)
            => await WithRegisteredUser(async (user) =>
            {
                await (await Context.GetDbConnectionAsync()).ExecuteAsync(
                    @"INSERT INTO phpbb_user_topic_post_number (user_id, topic_id, post_no) 
                           VALUES (@userId, @topicId, @userPostsPerPage)
                      ON DUPLICATE KEY UPDATE post_no = @userPostsPerPage",
                     new { user.UserId, topicId, userPostsPerPage }
                );

                ReloadCurrentUser();

                if (postId.HasValue)
                {
                    return RedirectToPage("ViewTopic", "ByPostId", new { postId = postId.Value, highlight = false });
                }
                else
                {
                    return RedirectToPage("ViewTopic", new { topicId = TopicId.Value, pageNum = 1 });
                }
            });

        public async Task<IActionResult> OnPostVote(int topicId, int[] votes, string queryString)
            => await WithRegisteredUser(user => WithValidTopic(topicId, async (_, topic) =>
            {
                var conn = await Context.GetDbConnectionAsync();
                
                var existingVotes = (await conn.QueryAsync<PhpbbPollVotes>("SELECT * FROM phpbb_poll_votes WHERE topic_id = @topicId AND vote_user_id = @UserId", new { topicId, user.UserId })).AsList();
                if (existingVotes.Count > 0 && topic.PollVoteChange == 0)
                {
                    ModelState.AddModelError(nameof(Poll), LanguageProvider.Errors[GetLanguage(), "CANT_CHANGE_VOTE"]);
                    return Page();
                }

                var noLongerVoted = from prev in existingVotes
                                    join cur in votes
                                    on prev.PollOptionId equals cur
                                    into joined
                                    from j in joined.DefaultIfEmpty()
                                    where j == default
                                    select prev.PollOptionId;
                await Task.WhenAll(
                    conn.ExecuteAsync(
                        "DELETE FROM phpbb_poll_votes WHERE topic_id = @topicId AND vote_user_id = @UserId AND poll_option_id IN @noLongerVoted",
                        new { topicId, user.UserId, noLongerVoted = noLongerVoted.DefaultIfEmpty() }
                    ),
                    conn.ExecuteAsync(
                        "UPDATE phpbb_poll_options SET poll_option_total = poll_option_total - 1 WHERE topic_id = @topicId AND poll_option_id = @vote",
                        noLongerVoted.Select(vote => new { topicId, vote })
                    )
                );

                var newVotes = from cur in votes
                               join prev in existingVotes
                               on cur equals prev.PollOptionId
                               into joined
                               from j in joined.DefaultIfEmpty()
                               where j == default
                               select cur;

                await Task.WhenAll(
                    conn.ExecuteAsync(
                        "INSERT INTO phpbb_poll_votes (topic_id, poll_option_id, vote_user_id, vote_user_ip) VALUES (@topicId, @vote, @UserId, @usrIp)",
                        newVotes.Select(vote => new { topicId, vote, user.UserId, usrIp = HttpContext.Connection.RemoteIpAddress.ToString() })
                    ),
                    conn.ExecuteAsync(
                        "UPDATE phpbb_poll_options SET poll_option_total = poll_option_total + 1 WHERE topic_id = @topicId AND poll_option_id = @vote",
                        newVotes.Select(vote => new { topicId, vote })
                    )
                );
                return Redirect($"./ViewTopic{HttpUtility.UrlDecode(queryString)}");
            }));

        public async Task<IActionResult> OnPostTopicModerator()
            => await WithModerator(ForumId ?? 0, async () =>
            {
                var logDto = new OperationLogDto
                {
                    Action = TopicAction.Value,
                    UserId = (GetCurrentUser()).UserId
                };

                var lang = GetLanguage();
                var (Message, IsSuccess) = TopicAction switch
                {
                    ModeratorTopicActions.MakeTopicNormal => await _moderatorService.ChangeTopicType(TopicId.Value, TopicType.Normal, logDto),
                    ModeratorTopicActions.MakeTopicImportant => await _moderatorService.ChangeTopicType(TopicId.Value, TopicType.Important, logDto),
                    ModeratorTopicActions.MakeTopicAnnouncement => await _moderatorService.ChangeTopicType(TopicId.Value, TopicType.Announcement, logDto),
                    ModeratorTopicActions.MakeTopicGlobal => await _moderatorService.ChangeTopicType(TopicId.Value, TopicType.Global, logDto),
                    ModeratorTopicActions.MoveTopic => await _moderatorService.MoveTopic(TopicId.Value, DestinationForumId.Value, logDto),
                    ModeratorTopicActions.LockTopic => await _moderatorService.LockUnlockTopic(TopicId.Value, true, logDto),
                    ModeratorTopicActions.UnlockTopic => await _moderatorService.LockUnlockTopic(TopicId.Value, false, logDto),
                    ModeratorTopicActions.DeleteTopic => await _moderatorService.DeleteTopic(TopicId.Value, logDto),
                    _ => throw new NotImplementedException($"Unknown action '{TopicAction}'")
                };

                if (TopicAction == ModeratorTopicActions.DeleteTopic)
                {
                    return RedirectToPage("ViewForum", new { ForumId });
                }
                else if (TopicAction == ModeratorTopicActions.MoveTopic && (IsSuccess ?? false))
                {
                    var destinations = await Task.WhenAll(
                        Utils.CompressAndEncode($"<a href=\"./ViewForum?forumId={DestinationForumId ?? 0}\">{LanguageProvider.BasicText[lang, "GO_TO_NEW_FORUM"]}</a>"),
                        Utils.CompressAndEncode($"<a href=\"./ViewTopic?topicId={TopicId}&pageNum={PageNum}\">{LanguageProvider.BasicText[lang, "GO_TO_LAST_TOPIC"]}</a>")
                    );
                    return RedirectToPage("Confirm", "DestinationConfirmation", new { destinations });
                }
                else
                {
                    ModeratorActionResult = $"<span style=\"margin-left: 30px\" class=\"{((IsSuccess ?? false) ? "success" : "fail")}\">{Message}</span>";
                    return await OnGet();
                }
            });

        public async Task<IActionResult> OnPostPostModerator()
            => await WithModerator(ForumId ?? 0, () => ModeratePosts());

        public async Task<IActionResult> OnPostDeleteMyMessage()
            => await WithRegisteredUser(async (user) =>
            {
                var lang = GetLanguage();
                if (await IsCurrentUserModeratorHere())
                {
                    return await ModeratePosts();
                }

                var postIds = GetModeratorPostIds();
                if (postIds.Length != 1 || PostAction != ModeratorPostActions.DeleteSelectedPosts)
                {
                    return RedirectToPage("Error", new { isUnauthorised = true });
                }

                var conn = await Context.GetDbConnectionAsync();

                var toDelete = await conn.QueryFirstOrDefaultAsync<PhpbbPosts>("SELECT * FROM phpbb_posts WHERE post_id = @postId", new { postId = postIds[0] });
                if (toDelete == null)
                {
                    return RedirectToPage("Error", new { isNotFound = true });
                }
                
                var lastPost = await conn.QueryFirstOrDefaultAsync<PhpbbPosts>("SELECT * FROM phpbb_posts WHERE topic_id = @topicId ORDER BY post_time DESC", new { toDelete.TopicId });
                var errorMessage = "<span class=\"validation\">&#x274C;&nbsp;{0}</span>";

                if (toDelete.PostTime < lastPost.PostTime)
                {
                    ModeratorActionResult = string.Format(errorMessage, LanguageProvider.Errors[lang, "POST_NO_LONGER_LAST"]);
                    PostId = postIds[0];
                    return await OnGet();
                }

                if (!(toDelete.PosterId == user.UserId && (user.PostEditTime == 0 || DateTime.UtcNow.Subtract(toDelete.PostTime.ToUtcTime()).TotalMinutes <= user.PostEditTime)))
                {
                    ModeratorActionResult = string.Format(errorMessage, LanguageProvider.Errors[lang, "EDIT_TIME_EXPIRED"]);
                    PostId = postIds[0]; 
                    return await OnGet();
                }

                var curTopic = await conn.QueryFirstOrDefaultAsync<PhpbbTopics>("SELECT * FROM phpbb_topics WHERE topic_id = @topicId", new { toDelete.TopicId });
                if (curTopic?.TopicStatus.ToBool() ?? false)
                {
                    ModeratorActionResult = string.Format(errorMessage, LanguageProvider.Errors[lang, "CANT_DELETE_POST_TOPIC_CLOSED", Casing.FirstUpper]);
                    PostId = postIds[0]; 
                    return await OnGet();
                }

                return await ModeratePosts();
            });

        public async Task<IActionResult> OnPostReportMessage(int? reportPostId, short? reportReasonId, string reportDetails)
            => await WithRegisteredUser((user) => WithValidPost(reportPostId ?? 0, async (_, _, _) =>
            {
                var conn = await Context.GetDbConnectionAsync();
                await conn.ExecuteAsync(
                    "INSERT INTO phpbb_reports (post_id, user_id, reason_id, report_text, report_time, report_closed) " +
                    "VALUES (@PostId, @UserId, @ReasonId, @ReportText, @ReportTime, 0)",
                    new
                    {
                        PostId = reportPostId.Value,
                        user.UserId,
                        ReasonId = reportReasonId.Value,
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
                    UserId = (GetCurrentUser()).UserId
                };
                var (_, nextRemaining) = await GetSelectedAndNextRemainingPostIds(reportPostId ?? 0);
                if (deletePost ?? false)
                {
                    var (Message, IsSuccess) = await _moderatorService.DeletePosts(new[] { reportPostId.Value }, logDto);
                    ModeratorActionResult = $"<span style=\"margin-left: 30px\" class=\"{((IsSuccess ?? false) ? "success" : "fail")}\">{Message}</span>";
                    PostId = nextRemaining;
                }
                else
                {
                    PostId = reportPostId;
                }

                var conn = await Context.GetDbConnectionAsync();
                await conn.ExecuteAsync(
                    "UPDATE phpbb_reports SET report_closed = 1 WHERE report_id = @reportId;",
                    new { reportId }
                );

                if (!(deletePost ?? false) && (redirectToEdit ?? false))
                {
                    var reportedPost = await conn.QueryFirstOrDefaultAsync<PhpbbPosts>("SELECT * FROM phpbb_posts WHERE post_id = @reportPostId", new { reportPostId });
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
                    UserId = (GetCurrentUser()).UserId
                };
                var (Message, IsSuccess) = await _moderatorService.DuplicatePost(postIdForDuplication, logDto);

                PostId = postIdForDuplication;
                if (!(IsSuccess ?? false))
                {
                    ModeratorActionResult = $"<span style=\"margin-left: 30px; color: red; display:block;\">{Message}</span>";
                }
                return await OnGetByPostId();
            });

        private async Task<IActionResult> ModeratePosts()
        {
            var lang = GetLanguage();
            var logDto = new OperationLogDto
            {
                Action = PostAction.Value,
                UserId = (GetCurrentUser()).UserId
            };
            var postIds = GetModeratorPostIds();
            var (Message, IsSuccess) = PostAction switch
            {
                ModeratorPostActions.DeleteSelectedPosts => await _moderatorService.DeletePosts(postIds, logDto),
                ModeratorPostActions.MoveSelectedPosts => await _moderatorService.MovePosts(postIds, DestinationTopicId, logDto),
                ModeratorPostActions.SplitSelectedPosts => await _moderatorService.SplitPosts(postIds, DestinationForumId, logDto),
                _ => throw new NotImplementedException($"Unknown action '{PostAction}'")
            };

            if ((IsSuccess ?? false) && PostAction == ModeratorPostActions.DeleteSelectedPosts && postIds.Length == 1 && ClosestPostId.HasValue)
            {
                PostId = ClosestPostId;
                return await OnGetByPostId();
            }

            if (IsSuccess ?? false)
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
                    destinations.Add(await Utils.CompressAndEncode($"<a href=\"./ViewTopic?postId={LatestSelected}&handler=byPostId\">{LanguageProvider.BasicText[lang, "GO_TO_NEW_TOPIC"]}</a>"));
                };

                if (NextRemaining != null)
                {
                    destinations.Add(await Utils.CompressAndEncode($"<a href=\"./ViewTopic?postId={NextRemaining}&handler=byPostId\">{LanguageProvider.BasicText[lang, "GO_TO_LAST_TOPIC"]}</a>"));
                }
                else
                {
                    destinations.Add(await Utils.CompressAndEncode($"<a href=\"./ViewForum?forumId={ForumId}\">{LanguageProvider.BasicText[lang, "GO_TO_LAST_FORUM"]}</a>"));
                }

                return RedirectToPage("Confirm", "DestinationConfirmation", new { destinations });
            }

            ModeratorActionResult = $"<span style=\"margin-left: 30px; color: red; display:block;\">{Message}</span>";
            return await OnGet();
        }

        public int[] GetModeratorPostIds()
        {
            if (PostIdsForModerator?.Any() ?? false)
            {
                return PostIdsForModerator;
            }
            return SelectedPostIds?.Split(',')?.Select(x => int.TryParse(x, out var val) ? val : 0)?.Where(x => x != 0)?.ToArray() ?? Array.Empty<int>();
        }

        private async Task GetPostsLazy(int? topicId, int? page, int? postId)
        {
            if (!(Posts?.Any() ?? false))
            {
                var user = GetCurrentUser();
                using var multi = await (await Context.GetDbConnectionAsync()).QueryMultipleAsync(
                    sql: "CALL get_posts_extended(@userId, @topicId, @page, @pageSize, @postId);", 
                    param: new 
                    { 
                        user.UserId, 
                        topicId, 
                        page,
                        pageSize = user.TopicPostsPerPage.TryGetValue(topicId ?? 0, out var val) ? val : null as int?,
                        postId 
                    }
                );

                Posts = (await multi.ReadAsync<PostDto>()).AsList();
                var dbAttachments = (await multi.ReadAsync<PhpbbAttachments>()).AsList();
                PageNum = unchecked((int)await multi.ReadSingleAsync<long>());
                _count = unchecked((int)await multi.ReadSingleAsync<long>());
                Reports = (await multi.ReadAsync<ReportDto>()).AsList();

                (CorrelationId, Attachments) = await _postService.CacheAttachmentsAndPrepareForDisplay(dbAttachments, GetLanguage(), Posts.Count, false);
            }
        }

        private async Task<(int? LatestSelected, int? NextRemaining)> GetSelectedAndNextRemainingPostIds(params int[] idsToInclude)
        {
            var conn = await Context.GetDbConnectionAsync();
            

            var latestSelectedPost = await conn.QueryFirstOrDefaultAsync<PhpbbPosts>(
                "SELECT * FROM phpbb_posts WHERE post_id IN @ids ORDER BY post_time DESC", 
                new { ids = idsToInclude.DefaultIfEmpty() }
            );

            var postIds = GetModeratorPostIds();
            var nextRemainingPost = await conn.QueryFirstOrDefaultAsync<PhpbbPosts>(
                "SELECT * FROM phpbb_posts WHERE topic_id = @topicId AND post_id NOT IN @ids AND post_time >= @time ORDER BY post_time ASC",
                new { topicId = TopicId.Value, ids = postIds.DefaultIfEmpty(), time = latestSelectedPost?.PostTime }
            ) ?? await conn.QueryFirstOrDefaultAsync<PhpbbPosts>(
                "SELECT * FROM phpbb_posts WHERE topic_id = @topicId AND post_id NOT IN @ids ORDER BY post_time DESC",
                new { topicId = TopicId.Value, ids = postIds.DefaultIfEmpty() }
            );
                
            return (latestSelectedPost?.PostId, nextRemainingPost?.PostId);
        }
    }
}