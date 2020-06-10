using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Serverless.Forum.Contracts;
using Serverless.Forum.ForumDb;
using Serverless.Forum.Pages.CustomPartials;
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

        public List<PostDto> Posts { get; private set; }

        public string TopicTitle { get; private set; }

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

        private PhpbbTopics _currentTopic;
        private List<PhpbbPosts> _dbPosts;
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
                
                await GetPostsLazy(null, null, PostId);

                TopicId = _currentTopic.TopicId;
                PageNum = _page.Value;
                return await OnGet();
            });

        public async Task<IActionResult> OnGet()
            => await WithValidTopic(TopicId ?? 0, async (curForum, curTopic) =>
            {
                _currentTopic = curTopic;

                if ((PageNum ?? 0) <= 0)
                {
                    return BadRequest($"'{PageNum}' nu este o valoare corectă pentru numărul paginii.");
                }

                ForumId = curForum?.ForumId;
                ForumTitle = HttpUtility.HtmlDecode(curForum?.ForumName ?? "untitled");

                await GetPostsLazy(TopicId, PageNum, null);

                Paginator = new Paginator(_count.Value, PageNum.Value, $"/ViewTopic?TopicId={TopicId}&PageNum=1", TopicId, await GetCurrentUserAsync());
                var pollTask = Task.Run(async () => Poll = await _postService.GetPoll(_currentTopic));
                var postProcessingTask = Task.Run(() =>
                {
                    Posts = (
                        from p in _dbPosts

                        join u in _context.PhpbbUsers.AsNoTracking()
                        on p.PosterId equals u.UserId
                        into joinedUsers

                        join a in _context.PhpbbAttachments.AsNoTracking()
                        on p.PostId equals a.PostMsgId
                        into joinedAttachments

                        from ju in joinedUsers.DefaultIfEmpty()

                        let lastEditUser = _context.PhpbbUsers.AsNoTracking().FirstOrDefault(u => u.UserId == p.PostEditUser)
                        let lastEditUsername = lastEditUser == null ? "Anonymous" : lastEditUser.Username
                        let report = _context.PhpbbReports.AsNoTracking().FirstOrDefault(r => r.PostId == p.PostId && r.ReportClosed == 0)

                        join r in _context.PhpbbRanks.AsNoTracking()
                        on ju.UserRank equals r.RankId
                        into joinedRanks

                        from jr in joinedRanks.DefaultIfEmpty()

                        select new PostDto
                        {
                            PostSubject = p.PostSubject,
                            PostText = p.PostText,
                            AuthorName = ju == null ? "Anonymous" : (ju.UserId == Constants.ANONYMOUS_USER_ID ? p.PostUsername : ju.Username),
                            AuthorId = ju == null ? 1 : (ju.UserId == Constants.ANONYMOUS_USER_ID ? null as int? : ju.UserId),
                            AuthorColor = ju == null ? null : ju.UserColour,
                            PostCreationTime = p.PostTime.ToUtcTime(),
                            PostModifiedTime = p.PostEditTime.ToUtcTime(),
                            PostId = p.PostId,
                            Attachments = joinedAttachments.Select(x => new _AttachmentPartialModel(x)).ToList(),
                            BbcodeUid = p.BbcodeUid,
                            Unread = IsPostUnread(p.TopicId, p.PostId),
                            AuthorHasAvatar = ju == null ? false : !string.IsNullOrWhiteSpace(ju.UserAvatar),
                            AuthorSignature = ju == null ? null : _renderingService.BbCodeToHtml(ju.UserSig, ju.UserSigBbcodeUid).RunSync(),
                            AuthorRank = jr == null ? null : jr.RankTitle,
                            LastEditTime = p.PostEditTime,
                            LastEditUser = lastEditUsername,
                            LastEditReason = p.PostEditReason,
                            EditCount = p.PostEditCount,
                            IP = p.PosterIp,
                            ReportId = report == null ? null as int? : report.ReportId,
                            ReportReasonId = report == null ? null as int? : report.ReasonId,
                            ReportDetails = report == null ? null as string : report.ReportText,
                            ReporterId = report == null ? null as int? : report.UserId
                        }
                    ).ToList();
                    TopicTitle = HttpUtility.HtmlDecode(_currentTopic.TopicTitle ?? "untitled");
                });

                await Task.WhenAll(pollTask, postProcessingTask);

                await _renderingService.ProcessPosts(Posts, PageContext, HttpContext, true);

                _utils.RunParallelDbTask(async (localContext) =>
                {
                    if (Posts.Any(p => p.Unread))
                    {
                        var existing = await localContext.PhpbbTopicsTrack.FirstOrDefaultAsync(t => t.UserId == CurrentUserId && t.TopicId == TopicId);
                        if (existing == null)
                        {
                            await localContext.PhpbbTopicsTrack.AddAsync(
                                new PhpbbTopicsTrack
                                {
                                    ForumId = ForumId.Value,
                                    MarkTime = DateTime.UtcNow.ToUnixTimestamp(),
                                    TopicId = TopicId.Value,
                                    UserId = CurrentUserId
                                }
                            );
                        }
                        else
                        {
                            existing.ForumId = ForumId.Value;
                            existing.MarkTime = DateTime.UtcNow.ToUnixTimestamp();
                        }
                    }
                    var curTopic = await localContext.PhpbbTopics.FirstOrDefaultAsync(t => t.TopicId == TopicId);
                    curTopic.TopicViews++;
                    await localContext.SaveChangesAsync();
                });

                return Page();
            });

        public async Task<IActionResult> OnPostPagination(int topicId, int userPostsPerPage, int? postId)
            => await WithRegisteredUser(async () =>
            {
                async Task save(ForumDbContext localContext)
                {
                    await localContext.SaveChangesAsync();
                    await ReloadCurrentUser();
                }

                var curValue = await _context.PhpbbUserTopicPostNumber.FirstOrDefaultAsync(ppp => ppp.UserId == CurrentUserId && ppp.TopicId == topicId);

                if (curValue == null)
                {
                    _context.PhpbbUserTopicPostNumber.Add(
                        new PhpbbUserTopicPostNumber
                        {
                            UserId = CurrentUserId,
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
            => await WithRegisteredUser(async () =>
            {
                var current = await _context.PhpbbPollVotes.Where(v => v.TopicId == topicId && v.VoteUserId == CurrentUserId).ToListAsync();
                var id = await _context.PhpbbPollVotes.AsNoTracking().MaxAsync(v => v.Id);
                if (current.Any())
                {
                    var topic = await _context.PhpbbTopics.AsNoTracking().FirstOrDefaultAsync(t => t.TopicId == topicId);
                    if (topic.PollVoteChange == 0)
                    {
                        return Forbid("Can't change votes for this poll.");
                    }
                    _context.PhpbbPollVotes.RemoveRange(current.Where(v => !votes.Contains(v.PollOptionId)));
                }
                foreach (var vote in votes)
                {
                    await _context.PhpbbPollVotes.AddAsync(new PhpbbPollVotes
                    {
                        Id = ++id,
                        PollOptionId = (byte)vote,
                        TopicId = topicId,
                        VoteUserId = CurrentUserId,
                        VoteUserIp = HttpContext.Connection.RemoteIpAddress.ToString()
                    });
                }
                await _context.SaveChangesAsync();
                return Redirect($"./ViewTopic{HttpUtility.UrlDecode(queryString)}");
            });

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
                else if (TopicAction == ModeratorTopicActions.MoveTopic)
                {
                    var destinations = new List<string>
                    {
                        await _utils.CompressAndUrlEncode($"<a href=\"./ViewForum?forumId={ForumId}\">Mergi la noul forum</a>"),
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
                    UserId = CurrentUserId,
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
            if (_dbPosts == null || _page == null || _count == null)
            {
                var results = await _postService.GetPostPageAsync(CurrentUserId, topicId, page, postId);
                _dbPosts = results.Posts;
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