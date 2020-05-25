using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
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
        public List<ForumDisplay> Forums { get; private set; }
        public List<TopicTransport> Topics { get; private set; }
        public _PaginationPartialModel Pagination { get; private set; }
        public string ForumTitle { get; private set; }
        public string ParentForumTitle { get; private set; }
        public int? ParentForumId { get; private set; }
        public int ForumId { get; private set; }

        public ViewForumModel(ForumDbContext context, ForumTreeService forumService, UserService userService, CacheService cacheService)
            : base(context, forumService, userService, cacheService)
        {
        }

        public async Task<IActionResult> OnGet(int forumId)
        {
            if (forumId == 0)
            {
                return RedirectToPage("/Index");
            }

            ForumId = forumId;
            var usr = await GetCurrentUserAsync();
            var thisForum = await (from f in _context.PhpbbForums.AsNoTracking()
                                   where f.ForumId == forumId
                                   select f).FirstOrDefaultAsync();

            var permissionError = await ForumAuthorizationResponses(thisForum).FirstOrDefaultAsync();
            if (permissionError != null)
            {
                return permissionError;
            }

            ForumTitle = HttpUtility.HtmlDecode(thisForum?.ForumName ?? "untitled");

            ParentForumId = thisForum.ParentId;
            ParentForumTitle = HttpUtility.HtmlDecode(await (from pf in _context.PhpbbForums.AsNoTracking()
                                                             where pf.ForumId == thisForum.ParentId
                                                             select pf.ForumName).FirstOrDefaultAsync() ?? "untitled");

            Forums = (await GetForum(forumId)).ChildrenForums.ToList();

            Topics = await (
                from t in _context.PhpbbTopics.AsNoTracking()
                where t.ForumId == forumId || t.TopicType == TopicType.Global
                orderby t.TopicLastPostTime descending

                group t by t.TopicType into groups
                orderby groups.Key descending
                select new TopicTransport
                {
                    TopicType = groups.Key,
                    Topics = from g in groups

                             let postCount = _context.PhpbbPosts.Count(p => p.TopicId == g.TopicId)
                             let pageSize = usr.TopicPostsPerPage.ContainsKey(g.TopicId) ? usr.TopicPostsPerPage[g.TopicId] : 14

                             select new TopicDisplay
                             {
                                 Id = g.TopicId,
                                 Title = HttpUtility.HtmlDecode(g.TopicTitle),
                                 LastPosterId = g.TopicLastPosterId == Constants.ANONYMOUS_USER_ID ? null as int? : g.TopicLastPosterId,
                                 LastPosterName = HttpUtility.HtmlDecode(g.TopicLastPosterName),
                                 LastPostTime = g.TopicLastPostTime.ToUtcTime(),
                                 PostCount = _context.PhpbbPosts.Count(p => p.TopicId == g.TopicId),
                                 Pagination = new _PaginationPartialModel($"/ViewTopic?topicId={g.TopicId}&pageNum=1", postCount, pageSize, 1),
                                 Unread = IsTopicUnread(g.TopicId),
                                 LastPosterColor = g.TopicLastPosterColour,
                                 LastPostId = g.TopicLastPostId
                             }
                }
            ).ToListAsync();

            return Page();
        }

        public async Task<IActionResult> OnPostForums(int forumId)
        {
                var usr = await GetCurrentUserAsync();
                var thisForum = await (from f in _context.PhpbbForums
                                       where f.ForumId == forumId
                                       select f).FirstOrDefaultAsync();

                var permissionError = await ForumAuthorizationResponses(thisForum).FirstOrDefaultAsync();
                if (permissionError != null)
                {
                    return permissionError;
                }

                var childForums = from f in _context.PhpbbForums
                                  where f.ParentId == forumId
                                  select f.ForumId;
                foreach (var child in childForums)
                {
                    await UpdateTracking(_context, child);
                }
                await _context.SaveChangesAsync();

            return await OnGet(forumId);
        }

        public async Task<IActionResult> OnPostTopics(int forumId)
        {
            var usr = await GetCurrentUserAsync();
            var thisForum = await (from f in _context.PhpbbForums
                                    where f.ForumId == forumId
                                    select f).FirstOrDefaultAsync();

            var permissionError = await ForumAuthorizationResponses(thisForum).FirstOrDefaultAsync();
            if (permissionError != null)
            {
                return permissionError;
            }

            await UpdateTracking(_context, forumId);
            await _context.SaveChangesAsync();
            return await OnGet(forumId);
        }

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
            await context.PhpbbForumsTrack.AddAsync(new PhpbbForumsTrack
            {
                ForumId = forumId,
                UserId = CurrentUserId,
                MarkTime = DateTime.UtcNow.ToUnixTimestamp()
            });
        }
    }
}