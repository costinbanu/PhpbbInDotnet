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

        public ViewForumModel(IConfiguration config, Utils utils, ForumTreeService forumService, UserService userService, CacheService cacheService)
            : base(config, utils, forumService, userService, cacheService)
        {

        }

        public async Task<IActionResult> OnGet(int forumId)
        {
            if (forumId == 0)
            {
                return RedirectToPage("/Index");
            }

            using (var context = new ForumDbContext(_config))
            {
                ForumId = forumId;
                var usr = await GetCurrentUserAsync();
                var thisForum = await (from f in context.PhpbbForums.AsNoTracking()
                                       where f.ForumId == forumId
                                       select f).FirstOrDefaultAsync();

                var permissionError = await ValidateForumPermissionsResponsesAsync(thisForum, forumId);
                if (permissionError != null)
                {
                    return permissionError;
                }

                ForumTitle = HttpUtility.HtmlDecode(thisForum?.ForumName ?? "untitled");

                ParentForumId = thisForum.ParentId;
                ParentForumTitle = HttpUtility.HtmlDecode(await (from pf in context.PhpbbForums.AsNoTracking()
                                                                 where pf.ForumId == thisForum.ParentId
                                                                 select pf.ForumName).FirstOrDefaultAsync() ?? "untitled");

                Forums = (await GetForum(forumId)).ChildrenForums.ToList();

                Topics = await (
                    from t in context.PhpbbTopics.AsNoTracking()
                    where t.ForumId == forumId
                    orderby t.TopicLastPostTime descending

                    group t by t.TopicType into groups
                    orderby groups.Key descending
                    select new TopicTransport
                    {
                        TopicType = groups.Key,
                        Topics = from g in groups

                                 join u in context.PhpbbUsers.AsNoTracking()
                                 on g.TopicLastPosterId equals u.UserId
                                 into joinedUsers

                                 from ju in joinedUsers.DefaultIfEmpty()

                                 let postCount = context.PhpbbPosts.Count(p => p.TopicId == g.TopicId)
                                 let pageSize = usr.TopicPostsPerPage.ContainsKey(g.TopicId) ? usr.TopicPostsPerPage[g.TopicId] : 14

                                 select new TopicDisplay
                                 {
                                     Id = g.TopicId,
                                     Title = HttpUtility.HtmlDecode(g.TopicTitle),
                                     LastPosterId = ju.UserId == 1 ? null as int? : ju.UserId,
                                     LastPosterName = HttpUtility.HtmlDecode(g.TopicLastPosterName),
                                     LastPostTime = g.TopicLastPostTime.ToUtcTime(),
                                     PostCount = context.PhpbbPosts.Count(p => p.TopicId == g.TopicId),
                                     Pagination = new _PaginationPartialModel($"/ViewTopic?topicId={g.TopicId}&pageNum=1", postCount, pageSize, 1),
                                     Unread = IsTopicUnread(g.TopicId),
                                     LastPosterColor = ju == null ? null : ju.UserColour,
                                     LastPostId = g.TopicLastPostId
                                 }
                    }
                ).ToListAsync();

                return Page();
            }
        }

        public async Task<IActionResult> OnPostForums(int forumId)
        {
            using (var context = new ForumDbContext(_config))
            {
                var usr = await GetCurrentUserAsync();
                var thisForum = await (from f in context.PhpbbForums
                                       where f.ForumId == forumId
                                       select f).FirstOrDefaultAsync();

                var permissionError = await ValidateForumPermissionsResponsesAsync(thisForum, forumId);
                if (permissionError != null)
                {
                    return permissionError;
                }

                var childForums = from f in context.PhpbbForums
                                  where f.ParentId == forumId
                                  select f.ForumId;
                foreach (var child in childForums)
                {
                    await UpdateTracking(context, child);
                }
                await context.SaveChangesAsync();
            }

            return await OnGet(forumId);
        }

        public async Task<IActionResult> OnPostTopics(int forumId)
        {
            using (var context = new ForumDbContext(_config))
            {
                var usr = await GetCurrentUserAsync();
                var thisForum = await (from f in context.PhpbbForums
                                       where f.ForumId == forumId
                                       select f).FirstOrDefaultAsync();

                var permissionError = await ValidateForumPermissionsResponsesAsync(thisForum, forumId);
                if (permissionError != null)
                {
                    return permissionError;
                }

                await UpdateTracking(context, forumId);
                await context.SaveChangesAsync();
            }

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

            //if (toRemove.Any())
            //{
                context.PhpbbTopicsTrack.RemoveRange(toRemove);
                await context.PhpbbForumsTrack.AddAsync(new PhpbbForumsTrack
                {
                    ForumId = forumId,
                    UserId = CurrentUserId,
                    MarkTime = DateTime.UtcNow.ToUnixTimestamp()
                });
            //}
        }
    }
}