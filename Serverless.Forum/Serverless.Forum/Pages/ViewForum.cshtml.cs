﻿using Microsoft.AspNetCore.Authentication;
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
using System.Threading.Tasks;
using System.Web;

namespace Serverless.Forum.Pages
{
    public class ViewForumModel : PageModel
    {
        public IEnumerable<ForumDisplay> Forums;
        public IEnumerable<TopicTransport> Topics;
        public _PaginationPartialModel Pagination;

        forumContext _dbContext;
        public ViewForumModel(forumContext context)
        {
            _dbContext = context;
        }

        public async Task<IActionResult> OnGet(int ForumId)
        {
            var user = User;
            if (!user.Identity.IsAuthenticated)
            {
                user = Acl.Instance.GetAnonymousUser(_dbContext);
                await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, user, new AuthenticationProperties
                {
                    AllowRefresh = true,
                    ExpiresUtc = DateTimeOffset.Now.AddMonths(1),
                    IsPersistent = true,
                });
            }

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
                return RedirectToPage("ForumLogin", new ForumLoginModel(_dbContext)
                {
                    ReturnUrl = HttpUtility.UrlEncode(HttpContext.Request.Path + HttpContext.Request.QueryString),
                    ForumId = ForumId,
                    ForumName = thisForum.ForumName
                });
            }

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
                                   let postCount = _dbContext.PhpbbPosts.Count(p => p.TopicId == g.TopicId)
                                   let pageSize = user.ToLoggedUser().TopicPostsPerPage.ContainsKey(g.TopicId) ? user.ToLoggedUser().TopicPostsPerPage[g.TopicId] : 14
                                   select new TopicDisplay
                                   {
                                       Id = g.TopicId,
                                       Title = HttpUtility.HtmlDecode(g.TopicTitle),
                                       LastPosterName = g.TopicLastPosterName,
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