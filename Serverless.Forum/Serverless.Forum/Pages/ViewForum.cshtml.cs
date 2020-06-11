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
    public class ViewForumModel : ModelWithLoggedUser
    {
        public List<ForumDto> Forums { get; private set; }
        public List<TopicTransport> Topics { get; private set; }
        public _PaginationPartialModel Pagination { get; private set; }
        public string ForumTitle { get; private set; }
        public string ParentForumTitle { get; private set; }
        public int? ParentForumId { get; private set; }
        public bool IsNewPostView { get; private set; } = false;

        [BindProperty(SupportsGet = true)]
        public int ForumId { get; set; }

        public ViewForumModel(ForumDbContext context, ForumTreeService forumService, UserService userService, CacheService cacheService)
            : base(context, forumService, userService, cacheService)
        {
        }

        public async Task<IActionResult> OnGet()
            => await WithValidForum(ForumId, async (thisForum) =>
            {
                ForumTitle = HttpUtility.HtmlDecode(thisForum?.ForumName ?? "untitled");

                ParentForumId = thisForum.ParentId;
                ParentForumTitle = HttpUtility.HtmlDecode((await _context.PhpbbForums.AsNoTracking().FirstOrDefaultAsync(pf => pf.ForumId == thisForum.ParentId))?.ForumName ?? "untitled");

                Forums = (await GetForum(ForumId)).ChildrenForums.ToList();
                var usr = await GetCurrentUserAsync();
                Topics = await (
                    from t in _context.PhpbbTopics.AsNoTracking()
                    where t.ForumId == ForumId || t.TopicType == TopicType.Global
                    orderby t.TopicLastPostTime descending

                    group t by t.TopicType into groups
                    orderby groups.Key descending
                    select new TopicTransport
                    {
                        TopicType = groups.Key,
                        Topics = from g in groups

                                 let postCount = _context.PhpbbPosts.Count(p => p.TopicId == g.TopicId)
                                 let pageSize = usr.TopicPostsPerPage.ContainsKey(g.TopicId) ? usr.TopicPostsPerPage[g.TopicId] : 14

                                 select new TopicDto
                                 {
                                     Id = g.TopicId,
                                     Title = HttpUtility.HtmlDecode(g.TopicTitle),
                                     LastPosterId = g.TopicLastPosterId == Constants.ANONYMOUS_USER_ID ? null as int? : g.TopicLastPosterId,
                                     LastPosterName = HttpUtility.HtmlDecode(g.TopicLastPosterName),
                                     LastPostTime = g.TopicLastPostTime.ToUtcTime(),
                                     PostCount = g.TopicReplies,
                                     Pagination = new _PaginationPartialModel($"/ViewTopic?topicId={g.TopicId}&pageNum=1", postCount, pageSize, 1, "PageNum"),
                                     Unread = IsTopicUnread(g.TopicId),
                                     LastPosterColor = g.TopicLastPosterColour,
                                     LastPostId = g.TopicLastPostId,
                                     ViewCount = g.TopicViews
                                 }
                    }
                ).ToListAsync();

                return Page();
            });

        public async Task<IActionResult> OnGetNewPosts()
            => await WithRegisteredUser(async () =>
            {
                var unread = GetUnreadTopicsAndParentsLazy();
                var usr = await GetCurrentUserAsync();
                Topics = new List<TopicTransport>
                {
                    new TopicTransport
                    {
                        Topics = from g in _context.PhpbbTopics.AsNoTracking()
                                 join ut in unread.Select(t => t.TopicId)
                                 on g.TopicId equals ut
                                 let postCount = _context.PhpbbPosts.Count(p => p.TopicId == g.TopicId)
                                 let pageSize = usr.TopicPostsPerPage.ContainsKey(g.TopicId) ? usr.TopicPostsPerPage[g.TopicId] : 14
                                 select new TopicDto
                                 {
                                     Id = g.TopicId,
                                     Title = HttpUtility.HtmlDecode(g.TopicTitle),
                                     LastPosterId = g.TopicLastPosterId == Constants.ANONYMOUS_USER_ID ? null as int? : g.TopicLastPosterId,
                                     LastPosterName = HttpUtility.HtmlDecode(g.TopicLastPosterName),
                                     LastPostTime = g.TopicLastPostTime.ToUtcTime(),
                                     PostCount = g.TopicReplies,
                                     Pagination = new _PaginationPartialModel($"/ViewTopic?topicId={g.TopicId}&pageNum=1", postCount, pageSize, 1, "PageNum"),
                                     Unread = true,
                                     LastPosterColor = g.TopicLastPosterColour,
                                     LastPostId = g.TopicLastPostId,
                                     ViewCount = g.TopicViews
                                 }
                    }
                };
                IsNewPostView = true;
                return Page();
            });

        public async Task<IActionResult> OnPostMarkForumsRead()
            => await WithRegisteredUser(async () => await WithValidForum(ForumId, async (_) =>
            {
                var childForums = from f in _context.PhpbbForums
                                  where f.ParentId == ForumId
                                  select f.ForumId;
                foreach (var child in childForums)
                {
                    await UpdateTracking(_context, child);
                }
                await _context.SaveChangesAsync();

                return Page();
            }));

        public async Task<IActionResult> OnPostMarkTopicsRead()
            => await WithRegisteredUser(async() => await WithValidForum(ForumId, async(_) =>
            {
                await UpdateTracking(_context, ForumId);
                return Page();
            }));

        private async Task UpdateTracking(ForumDbContext context, int forumId)
        {
            var toRemove = await (
                from t in context.PhpbbTopics

                where t.ForumId == forumId

                join tt in context.PhpbbTopicsTrack
                on t.TopicId equals tt.TopicId
                into joinedTopicTracks

                from jtt in joinedTopicTracks.DefaultIfEmpty()
                where jtt.UserId == CurrentUserId
                select jtt
            ).ToListAsync();

            context.PhpbbTopicsTrack.RemoveRange(toRemove);
            context.PhpbbForumsTrack.Remove(await context.PhpbbForumsTrack.FirstOrDefaultAsync(ft => ft.ForumId == ForumId && ft.UserId == CurrentUserId));
            await context.SaveChangesAsync();

            await context.PhpbbForumsTrack.AddAsync(new PhpbbForumsTrack
            {
                ForumId = forumId,
                UserId = CurrentUserId,
                MarkTime = DateTime.UtcNow.ToUnixTimestamp()
            });
            await _context.SaveChangesAsync();
        }
    }
}