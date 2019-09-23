using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Serverless.Forum.Contracts;
using Serverless.Forum.forum;
using Serverless.Forum.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Web;

namespace Serverless.Forum.Pages
{
    public class ViewForumModel : ModelWithLoggedUser
    {
        public IEnumerable<ForumDisplay> Forums;
        public IEnumerable<TopicTransport> Topics;
        public _PaginationPartialModel Pagination;
        public string ForumTitle;
        public string ParentForumTitle;
        public int? ParentForumId;

        public ViewForumModel(forumContext context) : base(context)
        {
        }

        public IActionResult OnGet(int ForumId)
        {
            var thisForum = (from f in _dbContext.PhpbbForums
                             where f.ForumId == ForumId
                             select f).FirstOrDefault();

            if (thisForum == null)
            {
                return NotFound();
            }

            if (!string.IsNullOrEmpty(thisForum.ForumPassword) &&
                (HttpContext.Session.GetInt32("ForumLogin") ?? -1) != ForumId)
            {
                if (CurrentUser.UserPermissions.Any(fp => fp.ForumId == ForumId && fp.AuthRoleId == 16))
                {
                    return RedirectToPage("Unauthorized");
                }
                else
                {
                    return RedirectToPage("ForumLogin", new ForumLoginModel(_dbContext)
                    {
                        ReturnUrl = HttpUtility.UrlEncode(HttpContext.Request.Path + HttpContext.Request.QueryString),
                        ForumId = ForumId,
                        ForumName = thisForum.ForumName
                    });
                }
            }

            ForumTitle = HttpUtility.HtmlDecode(thisForum?.ForumName ?? "untitled");

            ParentForumId = thisForum.ParentId;
            ParentForumTitle = HttpUtility.HtmlDecode((from pf in _dbContext.PhpbbForums
                                                       where pf.ForumId == thisForum.ParentId
                                                       select pf.ForumName).FirstOrDefault() ?? "untitled");

            Forums = from f in _dbContext.PhpbbForums
                     where f.ParentId == ForumId
                     orderby f.LeftId
                     select new ForumDisplay
                     {
                         Id = f.ForumId,
                         Name = HttpUtility.HtmlDecode(f.ForumName),
                         LastPosterName = f.ForumLastPosterName,
                         LastPostTime = f.ForumLastPostTime.TimestampToLocalTime()
                     };

            Topics = (from t in _dbContext.PhpbbTopics
                      where t.ForumId == ForumId
                      orderby t.TopicLastPostTime descending

                      group t by t.TopicType into groups
                      orderby groups.Key descending
                      select new TopicTransport
                      {
                          TopicType = groups.Key,
                          Topics = from g in groups

                                   join u in _dbContext.PhpbbUsers
                                   on g.TopicLastPosterId equals u.UserId 
                                   into joined

                                   from j in joined.DefaultIfEmpty()
                                   let postCount = _dbContext.PhpbbPosts.Count(p => p.TopicId == g.TopicId)
                                   let pageSize = CurrentUser.TopicPostsPerPage.ContainsKey(g.TopicId) ? CurrentUser.TopicPostsPerPage[g.TopicId] : 14
                                   select new TopicDisplay
                                   {
                                       Id = g.TopicId,
                                       Title = HttpUtility.HtmlDecode(g.TopicTitle),
                                       LastPosterId = j.UserId == 1 ? null as int? : j.UserId,
                                       LastPosterName = HttpUtility.HtmlDecode(g.TopicLastPosterName),
                                       LastPostTime = g.TopicLastPostTime.TimestampToLocalTime(),
                                       PostCount = _dbContext.PhpbbPosts.Count(p => p.TopicId == g.TopicId),
                                       Pagination = new _PaginationPartialModel
                                       {
                                           Link = $"/ViewTopic?TopicId={g.TopicId}&PageNum=1",
                                           Posts = postCount,
                                           PostsPerPage = pageSize,
                                           CurrentPage = 1
                                       }
                                   }
                      });

            return Page();
        }
    }
}