using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Serverless.Forum.Contracts;
using Serverless.Forum.forum;
using Serverless.Forum.Utilities;
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

        public ViewForumModel(IConfiguration config, Utils utils) : base(config, utils) { }

        public async Task<IActionResult> OnGet(int forumId)
        {
            using (var context = new forumContext(_config))
            {
                var thisForum = await (from f in context.PhpbbForums
                                       where f.ForumId == forumId
                                       select f).FirstOrDefaultAsync();

                if (thisForum == null)
                {
                    return NotFound($"Forumul {forumId} nu există.");
                }

                var usr = await GetCurrentUserAsync();

                if (!string.IsNullOrEmpty(thisForum.ForumPassword) &&
                    (HttpContext.Session.GetInt32("ForumLogin") ?? -1) != forumId)
                {
                    if (usr.UserPermissions.Any(fp => fp.ForumId == forumId && fp.AuthRoleId == 16))
                    {
                        return Unauthorized();
                    }
                    else
                    {
                        return RedirectToPage("ForumLogin", new ForumLoginModel(_config)
                        {
                            ReturnUrl = HttpUtility.UrlEncode(HttpContext.Request.Path + HttpContext.Request.QueryString),
                            ForumId = forumId,
                            ForumName = thisForum.ForumName
                        });
                    }
                }

                ForumTitle = HttpUtility.HtmlDecode(thisForum?.ForumName ?? "untitled");

                ParentForumId = thisForum.ParentId;
                ParentForumTitle = HttpUtility.HtmlDecode(await (from pf in context.PhpbbForums
                                                                 where pf.ForumId == thisForum.ParentId
                                                                 select pf.ForumName).FirstOrDefaultAsync() ?? "untitled");
                Forums = await (
                    from f in context.PhpbbForums
                    where f.ParentId == forumId

                    join u in context.PhpbbUsers
                    on f.ForumLastPosterId equals u.UserId
                    into joinedUsers

                    from ju in joinedUsers.DefaultIfEmpty()

                    orderby f.LeftId

                    select new ForumDisplay
                    {
                        Id = f.ForumId,
                        Name = HttpUtility.HtmlDecode(f.ForumName),
                        Description = HttpUtility.HtmlDecode(f.ForumDesc),
                        LastPosterId = f.ForumLastPosterId,
                        LastPosterName = HttpUtility.HtmlDecode(f.ForumLastPosterName),
                        LastPostTime = f.ForumLastPostTime.TimestampToLocalTime(),
                        Unread = IsForumUnread(f.ForumId),
                        LastPosterColor = ju == null ? null : ju.UserColour 
                    }
                ).ToListAsync();

                Topics = await (
                    from t in context.PhpbbTopics
                    where t.ForumId == forumId
                    orderby t.TopicLastPostTime descending

                    group t by t.TopicType into groups
                    orderby groups.Key descending
                    select new TopicTransport
                    {
                        TopicType = groups.Key,
                        Topics = from g in groups

                                 join u in context.PhpbbUsers
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
                                     LastPostTime = g.TopicLastPostTime.TimestampToLocalTime(),
                                     PostCount = context.PhpbbPosts.Count(p => p.TopicId == g.TopicId),
                                     Pagination = new _PaginationPartialModel($"/ViewTopic?topicId={g.TopicId}&pageNum=1", postCount, pageSize, 1),
                                     Unread = IsTopicUnread(g.TopicId),
                                     LastPosterColor = ju == null ? null : ju.UserColour
                                 }
                    }
                ).ToListAsync();

                return Page();
            }
        }
    }
}