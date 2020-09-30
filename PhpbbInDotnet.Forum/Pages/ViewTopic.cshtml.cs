using Dapper;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using PhpbbInDotnet.DTOs;
using PhpbbInDotnet.Database;
using PhpbbInDotnet.Database.Entities;
using PhpbbInDotnet.Services;
using PhpbbInDotnet.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;

namespace PhpbbInDotnet.Forum.Pages
{
    [ValidateAntiForgeryToken, ResponseCache(Location = ResponseCacheLocation.None, NoStore = true, Duration = 0)]
    public class ViewTopicModel : ModelWithLoggedUser
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
        public int[] PostIdsForModerator { get; set; }

        [BindProperty]
        public int? ClosestPostId { get; set; }

        public PollDto Poll { get; private set; }

        public List<PhpbbPosts> Posts { get; private set; }

        public string TopicTitle { get; private set; }
        public string ForumRulesLink { get; private set; }
        public string ForumRules { get; private set; }
        public string ForumRulesUid { get; private set; }
        public string ForumTitle { get; private set; }

        public string ModeratorActionResult { get; private set; }

        public Paginator Paginator { get; private set; }

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

        public IEnumerable<PhpbbUsers> Users { get; private set; }
        public IEnumerable<PhpbbUsers> LastEditUsers { get; private set; }
        public IEnumerable<PhpbbAttachments> Attachments { get; private set; }
        public IEnumerable<PhpbbReports> Reports { get; private set; }
        public IEnumerable<PhpbbRanks> Ranks { get; private set; }

        private PhpbbTopics _currentTopic;
        private PhpbbForums _currentForum;
        private int? _page;
        private int? _count;
        private readonly PostService _postService;
        private readonly ModeratorService _moderatorService;
        private readonly WritingToolsService _writingToolsService;

        public ViewTopicModel(ForumDbContext context, ForumTreeService forumService, UserService userService, CacheService cacheService, CommonUtils utils, PostService postService, 
            ModeratorService moderatorService, WritingToolsService writingToolsService, IConfiguration config, AnonymousSessionCounter sessionCounter)
            : base(context, forumService, userService, cacheService, config, sessionCounter, utils)
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
                await GetPostsLazy(null, null, PostId).ConfigureAwait(false);

                TopicId = _currentTopic.TopicId;
                PageNum = _page.Value;

                return await OnGet().ConfigureAwait(false);
            });

        public async Task<IActionResult> OnGet()
        {
            async Task<IActionResult> toDo(PhpbbForums curForum, PhpbbTopics curTopic)
            {
                _currentTopic = curTopic;

                if ((PageNum ?? 0) <= 0)
                {
                    return BadRequest($"'{PageNum}' nu este o valoare corectă pentru numărul paginii.");
                }

                ForumId = curForum?.ForumId;
                ForumTitle = HttpUtility.HtmlDecode(curForum?.ForumName ?? "untitled");

                await GetPostsLazy(TopicId, PageNum, null).ConfigureAwait(false);

                Paginator = new Paginator(_count.Value, PageNum.Value, $"/ViewTopic?TopicId={TopicId}&PageNum=1", TopicId, await GetCurrentUserAsync());
                Poll = await _postService.GetPoll(_currentTopic);

                using var connection = _context.Database.GetDbConnection();
                await connection.OpenIfNeeded();
                using var multi = await connection.QueryMultipleAsync(
                    "SELECT * FROM phpbb_users WHERE user_id IN @authors; " +
                    "SELECT * FROM phpbb_users WHERE user_id IN @editors; " +
                    "SELECT * FROM phpbb_attachments WHERE post_msg_id IN @posts ORDER BY attach_id DESC; " +
                    "SELECT * FROM phpbb_reports WHERE report_closed = 0 AND post_id IN @posts; " +
                    "SELECT r.* FROM phpbb_ranks r JOIN phpbb_users u on u.user_rank = r.rank_id WHERE u.user_id IN @authors;",
                    new
                    {
                        authors = Posts.Select(p => p.PosterId).DefaultIfEmpty(),
                        editors = Posts.Select(p => p.PostEditUser).DefaultIfEmpty(),
                        posts = Posts.Select(p => p.PostId).DefaultIfEmpty()
                    }
                );

                Users = await multi.ReadAsync<PhpbbUsers>();
                LastEditUsers = await multi.ReadAsync<PhpbbUsers>();
                Attachments = await multi.ReadAsync<PhpbbAttachments>(); //query should sort according to config['display_order']
                Reports = await multi.ReadAsync<PhpbbReports>();
                Ranks = await multi.ReadAsync<PhpbbRanks>();
                TopicTitle = HttpUtility.HtmlDecode(_currentTopic.TopicTitle ?? "untitled");
                ForumRulesLink = curForum.ForumRulesLink;
                ForumRules = curForum.ForumRules;
                ForumRulesUid = curForum.ForumRulesUid;

                if (await IsTopicUnread(ForumId ?? 0, TopicId ?? 0))
                {
                    var tracking = (await GetForumTree()).Tracking;
                    if (tracking.TryGetValue(ForumId ?? 0, out var tt) && tt.Count == 1 && Paginator.IsLastPage)
                    {
                        //current topic was the last unread in its forum, and it is the last page of unread messages, so mark the whole forum read
                        await MarkForumRead(curForum.ForumId);
                        
                        //current forum is the user's last unread forum, and it has just been read; set the mark time.
                        if (tracking.Count == 1)
                        {
                            await SetLastMark();
                        }
                    }
                    else
                    {
                        //there are other unread topics in this forum, or unread pages in this topic, so just mark the current page as read
                        var markTime = Posts.Max(p => p.PostTime);
                        var userId = (await GetCurrentUserAsync()).UserId;
                        var existing = await connection.ExecuteScalarAsync<long?>("SELECT mark_time FROM phpbb_topics_track WHERE user_id = @userId AND topic_id = @topicId", new { userId, topicId = TopicId.Value });
                        if (existing == null)
                        {
                            await connection.ExecuteAsync(
                                "INSERT INTO phpbb_topics_track (forum_id, mark_time, topic_id, user_id) VALUES (@forumId, @markTime, @topicId, @userId)",
                                new { forumId = ForumId.Value, markTime, topicId = TopicId.Value, userId }
                            );
                        }
                        else if (markTime > existing)
                        {
                            await connection.ExecuteAsync(
                                "UPDATE phpbb_topics_track SET forum_id = @forumId, mark_time = @markTime WHERE user_id = @userId AND topic_id = @topicId",
                                new { forumId = ForumId.Value, markTime, userId, topicId = TopicId.Value }
                            );
                        }
                    }
                }
                await connection.ExecuteAsync("UPDATE phpbb_topics SET topic_views = topic_views + 1 WHERE topic_id = @topicId", new { topicId = TopicId.Value });
                return Page();
            }

            if (_currentForum != null && _currentTopic != null)
            {
                return await toDo(_currentForum, _currentTopic);
            }
            else
            {
                return await WithValidTopic(TopicId ?? 0, async (curForum, curTopic) => await toDo(curForum, curTopic));
            }
        }

        public async Task<IActionResult> OnPostPagination(int topicId, int userPostsPerPage, int? postId)
            => await WithRegisteredUser(async (user) =>
            {
                async Task save(ForumDbContext localContext)
                {
                    await localContext.SaveChangesAsync();
                    await ReloadCurrentUser();
                }
                var curValue = await _context.PhpbbUserTopicPostNumber.FirstOrDefaultAsync(ppp => ppp.UserId == user.UserId && ppp.TopicId == topicId);

                if (curValue == null)
                {
                    _context.PhpbbUserTopicPostNumber.Add(
                        new PhpbbUserTopicPostNumber
                        {
                            UserId = (await GetCurrentUserAsync()).UserId,
                            TopicId = topicId,
                            PostNo = userPostsPerPage
                        }
                    );
                    await save(_context);
                }
                else if (curValue.PostNo != userPostsPerPage)
                {
                    curValue.PostNo = userPostsPerPage;
                    await save(_context);
                }
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
            => await WithRegisteredUser(async (user) => await WithValidTopic(topicId, async (_, topic) =>
            {
                using var conn = _context.Database.GetDbConnection();
                await conn.OpenIfNeeded();

                var existingVotes = (await conn.QueryAsync<PhpbbPollVotes>("SELECT * FROM phpbb_poll_votes WHERE topic_id = @topicId AND vote_user_id = @UserId", new { topicId, user.UserId })).AsList();
                if (existingVotes.Count > 0 && topic.PollVoteChange == 0)
                {
                    ModelState.AddModelError(nameof(Poll), "Votul nu poate fi schimbat!");
                    return Page();
                }

                var noLongerVoted = from prev in existingVotes
                                    join cur in votes
                                    on prev.PollOptionId equals cur
                                    into joined
                                    from j in joined.DefaultIfEmpty()
                                    where j == default
                                    select prev.PollOptionId;
                await conn.ExecuteAsync(
                    "DELETE FROM phpbb_poll_votes WHERE topic_id = @topicId AND vote_user_id = @UserId AND poll_option_id IN @noLongerVoted",
                    new { topicId, user.UserId, noLongerVoted = noLongerVoted.DefaultIfEmpty() }
                );
                foreach (var vote in noLongerVoted)
                {
                    await conn.ExecuteAsync(
                        "UPDATE phpbb_poll_options SET poll_option_total = poll_option_total - 1 WHERE topic_id = @topicId AND poll_option_id = @vote",
                        new { topicId, vote }
                    );
                }

                var newVotes = from cur in votes
                               join prev in existingVotes
                               on cur equals prev.PollOptionId
                               into joined
                               from j in joined.DefaultIfEmpty()
                               where j == default
                               select cur;

                foreach (var vote in newVotes)
                {
                    await conn.ExecuteAsync(
                        "INSERT INTO phpbb_poll_votes (topic_id, poll_option_id, vote_user_id, vote_user_ip) VALUES (@topicId, @vote, @UserId, @usrIp)",
                        new { topicId, vote, user.UserId, usrIp = HttpContext.Connection.RemoteIpAddress.ToString() }
                    );
                    await conn.ExecuteAsync(
                        "UPDATE phpbb_poll_options SET poll_option_total = poll_option_total + 1 WHERE topic_id = @topicId AND poll_option_id = @vote",
                        new { topicId, vote }
                    );
                }

                return Redirect($"./ViewTopic{HttpUtility.UrlDecode(queryString)}");
            }));

        public async Task<IActionResult> OnPostTopicModerator()
            => await WithModerator(async () =>
            {
                var (Message, IsSuccess) = TopicAction switch
                {
                    ModeratorTopicActions.MakeTopicNormal => await _moderatorService.ChangeTopicType(TopicId.Value, TopicType.Normal),
                    ModeratorTopicActions.MakeTopicImportant => await _moderatorService.ChangeTopicType(TopicId.Value, TopicType.Important),
                    ModeratorTopicActions.MakeTopicAnnouncement => await _moderatorService.ChangeTopicType(TopicId.Value, TopicType.Announcement),
                    ModeratorTopicActions.MakeTopicGlobal => await _moderatorService.ChangeTopicType(TopicId.Value, TopicType.Global),
                    ModeratorTopicActions.MoveTopic => await _moderatorService.MoveTopic(TopicId.Value, DestinationForumId.Value),
                    ModeratorTopicActions.LockTopic => await _moderatorService.LockUnlockTopic(TopicId.Value, true),
                    ModeratorTopicActions.UnlockTopic => await _moderatorService.LockUnlockTopic(TopicId.Value, false),
                    ModeratorTopicActions.DeleteTopic => await _moderatorService.DeleteTopic(TopicId.Value),
                    _ => throw new NotImplementedException($"Unknown action '{TopicAction}'")
                };

                if (TopicAction == ModeratorTopicActions.DeleteTopic)
                {
                    return RedirectToPage("ViewForum", new { ForumId });
                }
                else if (TopicAction == ModeratorTopicActions.MoveTopic && (IsSuccess ?? false))
                {
                    var destinations = new List<string>
                    {
                        await _utils.CompressAndEncode($"<a href=\"./ViewForum?forumId={DestinationForumId ?? 0}\">Mergi la noul forum</a>"),
                        await _utils.CompressAndEncode($"<a href=\"./ViewTopic?topicId={TopicId}&pageNum={PageNum}\">Mergi la ultimul subiect vizitat</a>")
                    };
                    return RedirectToPage("Confirm", "DestinationConfirmation", new { destinations });
                }
                else
                {
                    ModeratorActionResult = $"<span style=\"margin-left: 30px; color: {((IsSuccess ?? false) ? "darkgreen" : "red")}; display:block;\">{Message}</span>";
                    return await OnGet();
                }
            });

        public async Task<IActionResult> OnPostPostModerator()
            => await WithModerator(async () => await ModeratePosts());

        public async Task<IActionResult> OnPostDeleteMyMessage()
            => await WithRegisteredUser(async (user) =>
            {
                if (await IsCurrentUserModeratorHere())
                {
                    return await ModeratePosts();
                }

                if (PostIdsForModerator.Length != 1 || PostAction != ModeratorPostActions.DeleteSelectedPosts)
                {
                    return Unauthorized();
                }

                var toDelete = await _context.PhpbbPosts.AsNoTracking().FirstOrDefaultAsync(p => p.PostId == PostIdsForModerator[0]);
                var lastPosts = await _context.PhpbbPosts.AsNoTracking().Where(p => p.TopicId == toDelete.TopicId).OrderByDescending(p => p.PostTime).Take(2).ToListAsync();
                if (toDelete.PostTime < lastPosts[0].PostTime)
                {
                    ModelState.AddModelError(nameof(PostIdsForModerator), "Mesajul nu poate fi șters deoarece nu (mai) este ultimul din subiect!");
                    return await OnGet();
                }

                if (!(toDelete.PosterId == user.UserId && (user.PostEditTime == 0 || DateTime.UtcNow.Subtract(toDelete.PostTime.ToUtcTime()).TotalMinutes <= user.PostEditTime)))
                {
                    ModelState.AddModelError(nameof(PostIdsForModerator), "Mesajul nu poate fi șters deoarece a expirat timul limită de modificare a mesajelor!");
                    return await OnGet();
                }

                return await ModeratePosts(lastPosts[1].PostId);
            });

        public async Task<IActionResult> OnPostReportMessage(int? reportPostId, short? reportReasonId, string reportDetails)
            => await WithRegisteredUser(async (user) =>
            {
                var result = await _context.PhpbbReports.AddAsync(new PhpbbReports
                {
                    PostId = reportPostId.Value,
                    UserId = user.UserId,
                    ReasonId = reportReasonId.Value,
                    ReportText = _writingToolsService.PrepareTextForSaving(reportDetails),
                    ReportTime = DateTime.UtcNow.ToUnixTimestamp(),
                    ReportClosed = 0
                });
                result.Entity.ReportId = 0;
                var topic = await _context.PhpbbTopics.FirstOrDefaultAsync(t => t.TopicId == TopicId);
                topic.TopicReported = 1;
                var post = await _context.PhpbbPosts.FirstOrDefaultAsync(t => t.PostId == reportPostId);
                post.PostReported = 1;
                await _context.SaveChangesAsync();
                return await OnGet();
            });

        public async Task<IActionResult> OnPostManageReport(int? reportPostId, int? reportId, bool? redirectToEdit, bool? deletePost)
            => await WithModerator(async () =>
            {
                if (deletePost ?? false)
                {
                    var (LatestSelected, NextRemaining) = await GetSelectedAndNextRemainingPostIds(reportPostId ?? 0);
                    var (Message, IsSuccess) = await _moderatorService.DeletePosts(new[] { reportPostId.Value });
                    ModeratorActionResult = $"<span style=\"margin-left: 30px; color: {((IsSuccess ?? false) ? "darkgreen" : "red")}; display:block;\">{Message}</span>";
                }
                var report = await _context.PhpbbReports.FirstOrDefaultAsync(r => r.ReportId == reportId);
                if (report != null)
                {
                    report.ReportClosed = 1;
                }
                var topic = await _context.PhpbbTopics.FirstOrDefaultAsync(t => t.TopicId == TopicId);
                if (topic != null)
                {
                    topic.TopicReported = 0;
                }
                var post = await _context.PhpbbPosts.FirstOrDefaultAsync(t => t.TopicId == reportPostId);
                if (post != null)
                {
                    post.PostReported = 0;
                }
                await _context.SaveChangesAsync();
                if (!(deletePost ?? false) && (redirectToEdit ?? false))
                {
                    var reportedPost = await _context.PhpbbPosts.FirstOrDefaultAsync(p => p.PostId == reportPostId);
                    if (reportedPost == null)
                    {
                        return await OnGet();
                    }
                    return RedirectToPage("Posting", "editPost", new { reportedPost.ForumId, reportedPost.TopicId, reportedPost.PostId });
                }
                else
                {
                    return await OnGet();
                }
            });

        public string MapModeratorTopicActions(ModeratorTopicActions action)
            => action switch
            {
                ModeratorTopicActions.DeleteTopic => "Șterge subiect",
                ModeratorTopicActions.LockTopic => "Închide subiect",
                ModeratorTopicActions.MakeTopicAnnouncement => "Transformă subiectul în anunț",
                ModeratorTopicActions.MakeTopicGlobal => "Transformă subiectul în subiect global",
                ModeratorTopicActions.MakeTopicImportant => "Transformă subiectul în subiect important",
                ModeratorTopicActions.MakeTopicNormal => "Transformă subiectul în subiect normal",
                ModeratorTopicActions.MoveTopic => "Mută subiect",
                ModeratorTopicActions.UnlockTopic => "Deschide subiect",
                _ => throw new ArgumentException($"Unknown moderator topic action '{action}'", nameof(action))
            };

        public string MapModeratorPostActions(ModeratorPostActions action)
            => action switch
            {
                ModeratorPostActions.DeleteSelectedPosts => "Șterge mesajele selectate",
                ModeratorPostActions.MoveSelectedPosts => "Mută mesajele selectate",
                ModeratorPostActions.SplitSelectedPosts => "Desparte mesajele selectate într-un nou subiect",
                _ => throw new ArgumentException($"Unknown moderator post action '{action}'", nameof(action))
            };

        private async Task<IActionResult> ModeratePosts(int backToPost = 0)
        {
            var (Message, IsSuccess) = PostAction switch
            {
                ModeratorPostActions.DeleteSelectedPosts => await _moderatorService.DeletePosts(PostIdsForModerator),
                ModeratorPostActions.MoveSelectedPosts => await _moderatorService.MovePosts(PostIdsForModerator, DestinationTopicId),
                ModeratorPostActions.SplitSelectedPosts => await _moderatorService.SplitPosts(PostIdsForModerator, DestinationForumId),
                _ => throw new NotImplementedException($"Unknown action '{PostAction}'")
            };

            if (backToPost > 0)
            {
                PostId = backToPost;
                return await OnGetByPostId();
            }

            if (IsSuccess ?? false)
            {
                int? LatestSelected, NextRemaining;
                if (PostAction == ModeratorPostActions.DeleteSelectedPosts)
                {
                    (LatestSelected, NextRemaining) = (null, ClosestPostId);
                }
                else
                {
                    (LatestSelected, NextRemaining) = await GetSelectedAndNextRemainingPostIds(PostIdsForModerator);
                }
                var destinations = new List<string>();
                if (LatestSelected != null)
                {
                    destinations.Add(await _utils.CompressAndEncode($"<a href=\"./ViewTopic?postId={LatestSelected}&handler=byPostId\">Mergi la noul subiect</a>"));
                };

                if (NextRemaining != null)
                {
                    destinations.Add(await _utils.CompressAndEncode($"<a href=\"./ViewTopic?postId={NextRemaining}&handler=byPostId\">Mergi la ultimul subiect vizitat</a>"));
                }
                else
                {
                    destinations.Add(await _utils.CompressAndEncode($"<a href=\"./ViewForum?forumId={ForumId}\">Mergi la ultimul forum vizitat</a>"));
                }

                return RedirectToPage("Confirm", "DestinationConfirmation", new { destinations });
            }

            ModeratorActionResult = $"<span style=\"margin-left: 30px; color: red; display:block;\">{Message}</span>";
            return await OnGet();
        }

        private async Task GetPostsLazy(int? topicId, int? page, int? postId)
        {
            if (Posts == null || _page == null || _count == null)
            {
                (Posts, _page, _count) = await _postService.GetPostPageAsync((await GetCurrentUserAsync()).UserId, topicId, page, postId);
            }
        }

        private async Task<(int? LatestSelected, int? NextRemaining)> GetSelectedAndNextRemainingPostIds(params int[] idsToInclude)
        {
            var latestSelectedPost = await (
               from p in _context.PhpbbPosts.AsNoTracking()
               where idsToInclude.Contains(p.PostId)
               orderby p.PostTime descending
               select p
            ).FirstOrDefaultAsync();

            var nextRemainingPost = await (
                from p in _context.PhpbbPosts.AsNoTracking()
                where p.TopicId == TopicId.Value
                   && !PostIdsForModerator.Contains(p.PostId)
                   && latestSelectedPost != null
                   && p.PostTime >= latestSelectedPost.PostTime
                orderby p.PostTime ascending
                select p
            ).FirstOrDefaultAsync() ?? await (
                from p in _context.PhpbbPosts.AsNoTracking()
                where p.TopicId == TopicId.Value
                   && !PostIdsForModerator.Contains(p.PostId)
                orderby p.PostTime descending
                select p
            ).FirstOrDefaultAsync();

            return (latestSelectedPost?.PostId, nextRemainingPost?.PostId);
        }
    }
}