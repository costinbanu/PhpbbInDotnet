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
        public List<PostDisplay> Posts { get; private set; }
        public string TopicTitle { get; private set; }
        public string ForumTitle { get; private set; }
        public int? ForumId { get; private set; }
        public int? PostId { get; private set; }
        public bool? Highlight { get; private set; }
        [BindProperty]
        public int? TopicId => _currentTopic?.TopicId;
        [BindProperty]
        public int? PageNum { get; private set; }
        public bool IsLocked => (_currentTopic?.TopicStatus ?? 0) == 1;
        public PollDisplay Poll { get; private set; }
        public string ModeratorActionResult { get; set; }

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

        public async Task<IActionResult> OnGetByPostId(int postId, bool? highlight)
        {
            if (_currentTopic == null)
            {
                _currentTopic = await (
                    from p in _context.PhpbbPosts.AsNoTracking()
                    where p.PostId == postId

                    join t in _context.PhpbbTopics.AsNoTracking()
                    on p.TopicId equals t.TopicId
                    into joined

                    from j in joined
                    select j
                    ).FirstOrDefaultAsync();
            }

            if (_currentTopic == null)
            {
                return NotFound($"Mesajul {postId} nu există.");
            }

            await GetPostsLazy(null, null, postId);

            PostId = postId;
            Highlight = highlight;
            return await OnGet(_currentTopic.TopicId, _page.Value);
        }

        public async Task<IActionResult> OnGet(int topicId, int pageNum)
        {
            PhpbbForums parent = null;
            if (_currentTopic == null)
            {
                _currentTopic = await (from t in _context.PhpbbTopics.AsNoTracking()
                                       where t.TopicId == topicId
                                       select t).FirstOrDefaultAsync();
            }

            if (_currentTopic == null)
            {
                return NotFound($"Subiectul {topicId} nu există.");
            }

            parent = await (from f in _context.PhpbbForums.AsNoTracking()

                            join t in _context.PhpbbTopics.AsNoTracking()
                            on f.ForumId equals t.ForumId
                            into joined

                            from j in joined
                            where j.TopicId == topicId
                            select f).FirstOrDefaultAsync();

            ForumId = parent?.ForumId;
            PageNum = pageNum;

            var permissionError = await ForumAuthorizationResponses(parent).FirstOrDefaultAsync();
            if (permissionError != null)
            {
                return permissionError;
            }

            ForumTitle = HttpUtility.HtmlDecode(parent?.ForumName ?? "untitled");

            await GetPostsLazy(topicId, pageNum, null);
            await ComputePagination(_count.Value, pageNum, $"/ViewTopic?TopicId={topicId}&PageNum=1", topicId);

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
                    AuthorName = ju == null ? "Anonymous" : (ju.UserId == 1 ? p.PostUsername : ju.Username),
                    AuthorId = ju == null ? 1 : (ju.UserId == 1 ? null as int? : ju.UserId),
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
            return Page();
        }

        public async Task<IActionResult> OnPostPagination(int topicId, int userPostsPerPage, int? postId)
        {
            if (CurrentUserId == 1)
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
            if (CurrentUserId == 1)
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

        public async Task<IActionResult> OnPostModerator(ModeratorActions action, int[] posts, int? topicId, int? pageNum)
        {
            if (!await IsCurrentUserModeratorHereAsync())
            {
                return Forbid();
            }

            if (IsPostRelatedAction(action) && !posts.Any())
            {
                ModelState.AddModelError(nameof(ModeratorActionResult), $"Acțiunea {action} nu poate fi efectuată fără mesaje selectate.");
            }

            var (Message, IsSuccess) = action switch
            {
                ModeratorActions.MakeTopicNormal => await _moderatorService.ChangeTopicType(topicId.Value, TopicType.Normal),
                ModeratorActions.MakeTopicImportant => await _moderatorService.ChangeTopicType(topicId.Value, TopicType.Important),
                ModeratorActions.MakeTopicAnnouncement => await _moderatorService.ChangeTopicType(topicId.Value, TopicType.Announcement),
                ModeratorActions.MakeTopicGlobal => await _moderatorService.ChangeTopicType(topicId.Value, TopicType.Global),
                _ => throw new NotImplementedException()
            };

            if (!(IsSuccess ?? false))
            {
                ModeratorActionResult = null;
                ModelState.AddModelError(nameof(ModeratorActionResult), Message);
            }
            else
            {
                ModeratorActionResult = Message;
            }

            return await OnGet(topicId.Value, pageNum.Value);
        }

        private bool IsPostRelatedAction(ModeratorActions action)
            => new[] { ModeratorActions.DeleteSelectedPosts, ModeratorActions.MergeSelectedPosts, ModeratorActions.SplitSelectedPosts }.Contains(action);

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