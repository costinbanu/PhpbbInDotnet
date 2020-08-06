using Dapper;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Serverless.Forum.Contracts;
using Serverless.Forum.ForumDb;
using Serverless.Forum.ForumDb.Entities;
using Serverless.Forum.Services;
using Serverless.Forum.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;

namespace Serverless.Forum.Pages
{
    [ValidateAntiForgeryToken]
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
        private readonly Utils _utils;
        private readonly PostService _postService;
        private readonly ModeratorService _moderatorService;
        private readonly BBCodeRenderingService _renderingService;

        public ViewTopicModel(ForumDbContext context, ForumTreeService forumService, UserService userService, CacheService cacheService,
            Utils utils, PostService postService, ModeratorService moderatorService, BBCodeRenderingService renderingService)
            : base(context, forumService, userService, cacheService)
        {
            _utils = utils;
            _postService = postService;
            _moderatorService = moderatorService;
            _renderingService = renderingService;
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
                    "SELECT * FROM phpbb_attachments WHERE post_msg_id IN @posts; " +
                    "SELECT * FROM phpbb_reports WHERE report_closed = 0 AND post_id IN @posts; " +
                    "SELECT r.* FROM phpbb_ranks r JOIN phpbb_users u on u.user_rank = r.rank_id WHERE u.user_id IN @authors;",
                    new
                    {
                        authors = Posts.Select(p => p.PosterId),
                        editors = Posts.Select(p => p.PostEditUser),
                        posts = Posts.Select(p => p.PostId)
                    }
                );

                Users = await multi.ReadAsync<PhpbbUsers>();
                LastEditUsers = await multi.ReadAsync<PhpbbUsers>();
                Attachments = await multi.ReadAsync<PhpbbAttachments>();
                Reports = await multi.ReadAsync<PhpbbReports>();
                Ranks = await multi.ReadAsync<PhpbbRanks>();
                TopicTitle = HttpUtility.HtmlDecode(_currentTopic.TopicTitle ?? "untitled");
                ForumRulesLink = curForum.ForumRulesLink;
                ForumRules = curForum.ForumRules;
                ForumRulesUid = curForum.ForumRulesUid;

                if (await IsTopicUnread(ForumId ?? 0, TopicId ?? 0))
                {
                    if ((await GetForumTree()).Tracking.TryGetValue(ForumId ?? 0, out var tt) && tt.Count == 1)
                    {
                        //current topic was the last unread in its forum, mark whole forum read
                        await MarkForumRead(curForum.ForumId);
                    }
                    else
                    {
                        //there are other unread topics in this forum, just mark the current topic as read
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
            => await WithRegisteredUser(async () =>
            {
                async Task save(ForumDbContext localContext)
                {
                    await localContext.SaveChangesAsync();
                    await ReloadCurrentUser();
                }
                var usrId = (await GetCurrentUserAsync()).UserId;
                var curValue = await _context.PhpbbUserTopicPostNumber.FirstOrDefaultAsync(ppp => ppp.UserId == usrId && ppp.TopicId == topicId);

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
            => await WithRegisteredUser(async () => await WithValidTopic(topicId, async (_, topic) =>
            {
                var usrId = (await GetCurrentUserAsync()).UserId;
                using var conn = _context.Database.GetDbConnection();
                await conn.OpenIfNeeded();

                var count = await conn.ExecuteScalarAsync<int>("SELECT count(1) FROM phpbb_poll_votes WHERE topic_id = @topicId AND vote_user_id = @usrId", new { topicId, usrId });
                if (count > 0 && topic.PollVoteChange == 0)
                {
                    return Forbid("Can't change votes for this poll.");
                }

                await conn.ExecuteAsync("DELETE FROM phpbb_poll_votes WHERE topic_id = @topicId AND vote_user_id = @usrId AND poll_option_id NOT IN @votes", new { topicId, usrId, votes });

                foreach (var vote in votes)
                {
                    await conn.ExecuteAsync(
                        "INSERT INTO phpbb_poll_votes (topic_id, poll_option_id, vote_user_id, vote_user_ip) VALUES (@topicId, @vote, @usrId, @usrIp)",
                        new { topicId, vote, usrId, usrIp = HttpContext.Connection.RemoteIpAddress.ToString() }
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
                        await _utils.CompressAndUrlEncode($"<a href=\"./ViewForum?forumId={DestinationForumId ?? 0}\">Mergi la noul forum</a>"),
                        await _utils.CompressAndUrlEncode($"<a href=\"./ViewTopic?topicId={TopicId}&pageNum={PageNum}\">Mergi la ultimul subiect vizitat</a>")
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
            => await WithModerator(async () =>
            {
                var (Message, IsSuccess) = PostAction switch
                {
                    ModeratorPostActions.DeleteSelectedPosts => await _moderatorService.DeletePosts(PostIdsForModerator),
                    ModeratorPostActions.MoveSelectedPosts => await _moderatorService.MovePosts(PostIdsForModerator, DestinationTopicId.Value),
                    ModeratorPostActions.SplitSelectedPosts => await _moderatorService.SplitPosts(PostIdsForModerator, DestinationForumId.Value),
                    _ => throw new NotImplementedException($"Unknown action '{PostAction}'")
                };

                if (IsSuccess ?? false)
                {
                    var (LatestSelected, NextRemaining) = await GetSelectedAndNextRemainingPostIds(p => PostIdsForModerator.Contains(p.PostId));
                    var destinations = new List<string>();
                    if (LatestSelected != null)
                    {
                        destinations.Add(await _utils.CompressAndUrlEncode($"<a href=\"./ViewTopic?postId={LatestSelected}&handler=byPostId\">Mergi la noul subiect</a>"));
                    };

                    if (NextRemaining != null)
                    {
                        destinations.Add(await _utils.CompressAndUrlEncode($"<a href=\"./ViewTopic?postId={NextRemaining}&handler=byPostId\">Mergi la ultimul subiect vizitat</a>"));
                    }
                    else
                    {
                        destinations.Add(await _utils.CompressAndUrlEncode($"<a href=\"./ViewForum?forumId={ForumId}\">Mergi la ultimul forum vizitat</a>"));
                    }

                    return RedirectToPage("Confirm", "DestinationConfirmation", new { destinations });
                }

                ModeratorActionResult = $"<span style=\"margin-left: 30px; color: red; display:block;\">{Message}</span>";
                return await OnGet();
            });

        public async Task<IActionResult> OnPostReportMessage(int? reportPostId, short? reportReasonId, string reportDetails)
            => await WithRegisteredUser(async () =>
            {
                var result = await _context.PhpbbReports.AddAsync(new PhpbbReports
                {
                    PostId = reportPostId.Value,
                    UserId = (await GetCurrentUserAsync()).UserId,
                    ReasonId = reportReasonId.Value,
                    ReportText = reportDetails ?? string.Empty,
                    ReportTime = DateTime.UtcNow.ToUnixTimestamp(),
                    ReportClosed = 0
                });
                result.Entity.ReportId = 0;
                await _context.SaveChangesAsync();
                return await OnGet();
            });

        public async Task<IActionResult> OnPostManageReport(int? reportPostId, int? reportId, bool? redirectToEdit, bool? deletePost)
            => await WithModerator(async () =>
            {
                if (deletePost ?? false)
                {
                    var (LatestSelected, NextRemaining) = await GetSelectedAndNextRemainingPostIds(p => p.PostId == reportPostId);
                    var (Message, IsSuccess) = await _moderatorService.DeletePosts(new[] { reportPostId.Value });
                    ModeratorActionResult = $"<span style=\"margin-left: 30px; color: {((IsSuccess ?? false) ? "darkgreen" : "red")}; display:block;\">{Message}</span>";
                }
                var report = await _context.PhpbbReports.FirstOrDefaultAsync(r => r.ReportId == reportId);
                report.ReportClosed = 1;
                await _context.SaveChangesAsync();
                if (!(deletePost ?? false) && (redirectToEdit ?? false))
                {
                    var reportedPost = await _context.PhpbbPosts.FirstOrDefaultAsync(p => p.PostId == reportPostId);
                    return RedirectToPage("Posting", "editPost", new { reportedPost.ForumId, reportedPost.TopicId, reportedPost.PostId });
                }
                else
                {
                    return await OnGet();
                }
            });

        // requires package NReco.ImageGenerator
        //        public async Task<IActionResult> OnPostTakeSnapshot(string html)
        //            => await WithModerator(async () =>
        //            {
        //                var completeHtml = 
        //$@" <html>
        //        <head>
        //            <link rel=""stylesheet"" href=""{GetAbsoluteUri("~/css/site.css")}"" />
        //            <link rel=""stylesheet"" href=""{GetAbsoluteUri("~/lib/bootstrap/dist/css/bootstrap.css")}"" />
        //            <link rel=""stylesheet"" href=""{GetAbsoluteUri("~/css/pagination.css")}"" />
        //            <link rel=""stylesheet"" href=""{GetAbsoluteUri("~/css/posting.css")}"" />
        //            <script type=""text/javascript"" src=""{GetAbsoluteUri("~/js/viewTopic.js")}""></script>
        //        </head>
        //        <body>
        //            <div class=""container body-content size1300"">
        //                {html}
        //            </div>
        //        </body>
        //    </html>";

        //                var converter = FormatterServices.GetUninitializedObject(typeof(HtmlToImageConverter)) as HtmlToImageConverter;
        //                converter.ToolPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "wkhtmltoimage");
        //                converter.WkHtmlToImageExeName = "wkhtmltoimage.exe";
        //                converter.ProcessPriority = ProcessPriorityClass.Normal;
        //                converter.Zoom = 1f;
        //                converter.Width = 0;
        //                converter.Height = 0;
        //                var img = converter.GenerateImage(completeHtml, ImageFormat.Jpeg);
        //                return await Task.FromResult(Content(Convert.ToBase64String(img)));
        //            });
        //private string GetAbsoluteUri(string relativeUri) 
        //    => HttpContext.Request.Scheme + "://" + HttpContext.Request.Host + Url.Content(relativeUri);


        private async Task GetPostsLazy(int? topicId, int? page, int? postId)
        {
            if (Posts == null || _page == null || _count == null)
            {
                var results = await _postService.GetPostPageAsync((await GetCurrentUserAsync()).UserId, topicId, page, postId);
                Posts = results.Posts;
                _page = results.Page;
                _count = results.Count;
            }
        }

        private async Task<(int? LatestSelected, int? NextRemaining)> GetSelectedAndNextRemainingPostIds(Func<PhpbbPosts, bool> latestSelectedFilter)
        {
            var latestSelectedPost = await (
               from p in _context.PhpbbPosts.AsNoTracking()
               where latestSelectedFilter(p)
               group p by p.PostTime into groups
               orderby groups.Key descending
               select groups.FirstOrDefault()
            ).FirstOrDefaultAsync();

            var nextRemainingPost = await (
                from p in _context.PhpbbPosts.AsNoTracking()
                where p.TopicId == TopicId.Value
                   && !PostIdsForModerator.Contains(p.PostId)
                   && latestSelectedPost != null
                   && p.PostTime >= latestSelectedPost.PostTime
                group p by p.PostTime into groups
                orderby groups.Key ascending
                select groups.FirstOrDefault()
            ).FirstOrDefaultAsync() ?? await (
                from p in _context.PhpbbPosts.AsNoTracking()
                where p.TopicId == TopicId.Value
                   && !PostIdsForModerator.Contains(p.PostId)
                group p by p.PostTime into groups
                orderby groups.Key descending
                select groups.FirstOrDefault()
            ).FirstOrDefaultAsync();

            return (latestSelectedPost?.PostId, nextRemainingPost?.PostId);
        }
    }
}