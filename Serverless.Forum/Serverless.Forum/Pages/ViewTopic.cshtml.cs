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
    public class ViewTopicModel : ModelWithPagination
    {
        public List<PostDisplay> Posts { get; set; }

        public string TopicTitle { get; set; }

        public string ForumTitle { get; set; }

        public int? ForumId { get; set; }
        
        [BindProperty(SupportsGet = true)]
        public int? PostId { get; set; }
        
        [BindProperty(SupportsGet = true)]
        public bool? Highlight { get; set; }

        [BindProperty(SupportsGet = true)]
        public int? TopicId { get; set; }

        [BindProperty(SupportsGet = true)]
        public int? PageNum { get; set; }

        public bool IsLocked => (_currentTopic?.TopicStatus ?? 0) == 1;

        public PollDisplay Poll { get; set; }

        public string ModeratorActionResult { get; set; }

        [BindProperty(SupportsGet = true)]
        public int? DestinationForumId { get; set; }

        [BindProperty(SupportsGet = true)]
        public int? DestinationTopicId { get; set; }

        [BindProperty(SupportsGet = true)]
        public ModeratorTopicActions? TopicAction { get; set; }

        [BindProperty(SupportsGet = true)]
        public ModeratorPostActions? PostAction { get; set; }

        [BindProperty]
        public int[] PostIdsForModerator { get; set; }

        public bool ShowTopic => TopicAction == ModeratorTopicActions.MoveTopic && (
            (ModelState[nameof(DestinationForumId)]?.Errors?.Any() ?? false) ||
            DestinationForumId.HasValue
        );

        public bool ShowPostTopic =>PostAction == ModeratorPostActions.MoveSelectedPosts && (
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

        private PhpbbTopics _currentTopic;
        private List<PhpbbPosts> _dbPosts;
        private int? _page;
        private int? _count;
        private readonly PostService _postService;
        private readonly ModeratorService _moderatorService;
        private readonly BBCodeRenderingService _renderingService;

        public ViewTopicModel(Utils utils, ForumDbContext context, ForumTreeService forumService, UserService userService, CacheService cacheService, 
            PostService postService, ModeratorService moderatorService, BBCodeRenderingService renderingService)
            : base(utils, context, forumService, userService, cacheService)
        {
            _postService = postService;
            _moderatorService = moderatorService;
            _renderingService = renderingService;
        }

        public async Task<IActionResult> OnGetByPostId()
        {
            if (_currentTopic == null)
            {
                _currentTopic = await (
                    from p in _context.PhpbbPosts.AsNoTracking()
                    where p.PostId == PostId

                    join t in _context.PhpbbTopics.AsNoTracking()
                    on p.TopicId equals t.TopicId
                    into joined

                    from j in joined
                    select j
                    ).FirstOrDefaultAsync();
            }

            if (_currentTopic == null)
            {
                return NotFound($"Mesajul '{PostId}' nu există.");
            }

            await GetPostsLazy(null, null, PostId);

            TopicId = _currentTopic.TopicId;
            PageNum = _page.Value;
            return await OnGet();
        }

        public async Task<IActionResult> OnGet()
        {
            PhpbbForums parent = null;
            if (_currentTopic == null)
            {
                _currentTopic = await (from t in _context.PhpbbTopics.AsNoTracking()
                                       where t.TopicId == TopicId
                                       select t).FirstOrDefaultAsync();
            }

            if (_currentTopic == null)
            {
                return NotFound($"Subiectul '{TopicId}' nu există.");
            }

            if ((PageNum ?? 0) <= 0)
            {
                return BadRequest($"'{PageNum}' nu este o valoare corectă pentru numărul paginii.");
            }

            parent = await (from f in _context.PhpbbForums.AsNoTracking()

                            join t in _context.PhpbbTopics.AsNoTracking()
                            on f.ForumId equals t.ForumId
                            into joined

                            from j in joined
                            where j.TopicId == TopicId
                            select f).FirstOrDefaultAsync();

            ForumId = parent?.ForumId;

            var permissionError = await ForumAuthorizationResponses(parent).FirstOrDefaultAsync();
            if (permissionError != null)
            {
                return permissionError;
            }

            ForumTitle = HttpUtility.HtmlDecode(parent?.ForumName ?? "untitled");

            await GetPostsLazy(TopicId, PageNum, null);
            await ComputePagination(_count.Value, PageNum.Value, $"/ViewTopic?TopicId={TopicId}&PageNum=1", TopicId);

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

                join r in _context.PhpbbRanks.AsNoTracking()
                on ju.UserRank equals r.RankId
                into joinedRanks

                from jr in joinedRanks.DefaultIfEmpty()

                select new PostDisplay
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
                    AuthorSignature = ju == null ? null : _renderingService.BbCodeToHtml(ju.UserSig, ju.UserSigBbcodeUid),
                    AuthorRank = jr == null ? null : jr.RankTitle,
                    LastEditTime = p.PostEditTime,
                    LastEditUser = lastEditUsername,
                    LastEditReason = p.PostEditReason,
                    EditCount = p.PostEditCount
                }
            ).ToList();
            await _renderingService.ProcessPosts(Posts, PageContext, HttpContext, true);
            TopicTitle = HttpUtility.HtmlDecode(_currentTopic.TopicTitle ?? "untitled");

            await GetPoll();

            if (Posts.Any(p => p.Unread))
            {
                var existing = await _context.PhpbbTopicsTrack.FirstOrDefaultAsync(t => t.UserId == CurrentUserId && t.TopicId == TopicId);
                if (existing == null)
                {
                    await _context.PhpbbTopicsTrack.AddAsync(
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
                await _context.SaveChangesAsync();
            }
            //ModelState[nameof(ModeratorActionResult)].Errors.Clear();
            return Page();
        }

        public async Task<IActionResult> OnPostPagination(int topicId, int userPostsPerPage, int? postId)
        {
            if (CurrentUserId == Constants.ANONYMOUS_USER_ID)
            {
                return Forbid();
            }

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
        }

        public async Task<IActionResult> OnPostVote(int topicId, int[] votes, string queryString)
        {
            if (CurrentUserId == Constants.ANONYMOUS_USER_ID)
            {
                return Forbid();
            }

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
        }

        public async Task<IActionResult> OnPostTopicModerator()
        {
            if (!await IsCurrentUserModeratorHereAsync())
            {
                return Forbid();
            }

            if (TopicAction == ModeratorTopicActions.MoveTopic && await _context.PhpbbForums.AsNoTracking().FirstOrDefaultAsync(f => f.ForumId == DestinationForumId) == null)
            {
                ModelState.AddModelError(nameof(DestinationForumId), $"Forumul '{DestinationForumId}' nu există.");
                return await OnGet();
            }

            var (Message, IsSuccess) = TopicAction switch
            {
                ModeratorTopicActions.MakeTopicNormal => await _moderatorService.ChangeTopicType(TopicId.Value, TopicType.Normal),
                ModeratorTopicActions.MakeTopicImportant => await _moderatorService.ChangeTopicType(TopicId.Value, TopicType.Important),
                ModeratorTopicActions.MakeTopicAnnouncement => await _moderatorService.ChangeTopicType(TopicId.Value, TopicType.Announcement),
                ModeratorTopicActions.MakeTopicGlobal => await _moderatorService.ChangeTopicType(TopicId.Value, TopicType.Global),
                _ => throw new NotImplementedException()
            };

            if (!(IsSuccess ?? false))
            {
                ModeratorActionResult = $"<span style=\"margin-left: 30px; color: red; display:block;\">{Message}</span>";
            }
            else
            {
                ModeratorActionResult = $"<span style=\"margin-left: 30px; color: darkgreen; display:block;\">{Message}</span>";
            }

            return await OnGet();
        }

        public async Task<IActionResult> OnPostPostModerator()
        {
            if (!await IsCurrentUserModeratorHereAsync())
            {
                return Forbid();
            }

            if (!PostIdsForModerator.Any())
            {
                ModelState.AddModelError(nameof(PostIdsForModerator/*ErrorDummy*/), $"Acțiunea {PostAction} nu poate fi efectuată fără mesaje selectate.");
                return await OnGet();
            }

            if (PostAction == ModeratorPostActions.SplitSelectedPosts && await _context.PhpbbForums.AsNoTracking().FirstOrDefaultAsync(f => f.ForumId == DestinationForumId) == null)
            {
                ModelState.AddModelError(nameof(DestinationForumId), $"Forumul '{DestinationForumId}' nu există.");
                return await OnGet();
            }

            if (PostAction == ModeratorPostActions.MoveSelectedPosts && await _context.PhpbbTopics.AsNoTracking().FirstOrDefaultAsync(f => f.TopicId == DestinationTopicId) == null)
            {
                ModelState.AddModelError(nameof(DestinationTopicId), $"Subiectul '{DestinationTopicId}' nu există.");
                return await OnGet();
            }

            //var (Message, IsSuccess) = action switch
            //{
            //    ModeratorTopicActions.MakeTopicNormal => await _moderatorService.ChangeTopicType(topicId.Value, TopicType.Normal),
            //    ModeratorTopicActions.MakeTopicImportant => await _moderatorService.ChangeTopicType(topicId.Value, TopicType.Important),
            //    ModeratorTopicActions.MakeTopicAnnouncement => await _moderatorService.ChangeTopicType(topicId.Value, TopicType.Announcement),
            //    ModeratorTopicActions.MakeTopicGlobal => await _moderatorService.ChangeTopicType(topicId.Value, TopicType.Global),
            //    _ => throw new NotImplementedException()
            //};

            //if (!(IsSuccess ?? false))
            //{
            //    ModeratorActionResult = null;
            //    ModelState.AddModelError(nameof(ModeratorActionResult), Message);
            //}
            //else
            //{
            //    ModeratorActionResult = Message;
            //}

            return await OnGet();
        }

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

        private async Task GetPoll()
        {
            var tid = TopicId ?? 0;
            //var dbPollOptions = await _context.PhpbbPollOptions.AsNoTracking().Where(o => o.TopicId == tid).ToListAsync();

            //if (!dbPollOptions.Any() && string.IsNullOrWhiteSpace(_currentTopic.PollTitle) && _currentTopic.PollStart == 0)
            //{
            //    return;
            //}
            Poll = new PollDisplay
            {
                PollTitle = _currentTopic.PollTitle,
                PollStart = _currentTopic.PollStart.ToUtcTime(),
                PollDurationSecons = _currentTopic.PollLength,
                PollMaxOptions = _currentTopic.PollMaxOptions,
                TopicId = tid,
                VoteCanBeChanged = _currentTopic.PollVoteChange == 1,
                PollOptions = await (
                    from o in _context.PhpbbPollOptions.AsNoTracking()
                    where o.TopicId == tid
                    let voters = from v in _context.PhpbbPollVotes.AsNoTracking()
                                 where o.PollOptionId == v.PollOptionId && o.TopicId == v.TopicId
                                 join u in _context.PhpbbUsers.AsNoTracking()
                                 on v.VoteUserId equals u.UserId
                                 into joinedUsers

                                 from ju in joinedUsers.DefaultIfEmpty()
                                 where ju != null
                                 select new PollOptionVoter { UserId = ju.UserId, Username = ju.Username }

                    select new PollOption
                    {
                        PollOptionId = o.PollOptionId,
                        PollOptionText = o.PollOptionText,
                        TopicId = o.TopicId,
                        PollOptionVoters = voters.ToList() 
                    }
                ).ToListAsync()
            };

            if(!Poll.PollOptions.Any())
            {
                Poll = null;
            }
        }
    }
}