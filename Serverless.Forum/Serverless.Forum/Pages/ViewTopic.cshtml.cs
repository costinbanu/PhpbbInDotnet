using CodeKicker.BBCode;
using JW;
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
using System.Security.Claims;
using System.Threading.Tasks;
using System.Web;

namespace Serverless.Forum.Pages
{
    public class ViewTopicModel : PageModel
    {
        public IEnumerable<PostDisplay> Posts;
        public string TopicTitle;
        public string ForumTitle;
        public int? ForumId;
        public _PaginationPartialModel Pagination;
        public int? PostId;

        forumContext _dbContext;

        public ViewTopicModel(forumContext context, IHttpContextAccessor httpContext)
        {
            _dbContext = context;
        }

        public async Task<IActionResult> OnGetByPostId(int PostId)
        {
            var topicId = (from p in _dbContext.PhpbbPosts
                           where p.PostId == PostId
                           select (int?)p.TopicId).FirstOrDefault();

            if (topicId == null)
            {
                return NotFound();
            }

            var user = await GetUser().ConfigureAwait(false);
            var pageSize = user.ToLoggedUser().TopicPostsPerPage.ContainsKey(topicId.Value) ? user.ToLoggedUser().TopicPostsPerPage[topicId.Value] : 14;

            var posts = GetPosts(topicId.Value).Select(p => p.Id).ToList();
            var index = posts.IndexOf(PostId) + 1;
            var pageNum = (index / pageSize) + (index % pageSize == 0 ? 0 : 1);

            this.PostId = PostId;

            return await OnGet(topicId.Value, pageNum).ConfigureAwait(false);
        }

        public async Task<IActionResult> OnGet(int TopicId, int PageNum, int? pageSize = null)
        {
            if (pageSize is null)
            {
                var user = await GetUser().ConfigureAwait(false);
                pageSize = user.ToLoggedUser().TopicPostsPerPage.ContainsKey(TopicId) ? user.ToLoggedUser().TopicPostsPerPage[TopicId] : 14;
            }

            var parent = (from f in _dbContext.PhpbbForums

                          join t in _dbContext.PhpbbTopics
                          on f.ForumId equals t.ForumId
                          into joined

                          from j in joined
                          where j.TopicId == TopicId
                          select f).FirstOrDefault();

            if (!string.IsNullOrEmpty(parent.ForumPassword) &&
                (HttpContext.Session.GetInt32("ForumLogin") ?? -1) != parent.ForumId)
            {
                return RedirectToPage("ForumLogin", new ForumLoginModel(_dbContext)
                {
                    ReturnUrl = HttpUtility.UrlEncode(HttpContext.Request.Path + HttpContext.Request.QueryString),
                    ForumId = parent.ForumId,
                    ForumName = parent.ForumName
                });
            }

            ForumTitle = parent?.ForumName;
            ForumId = parent?.ForumId;

            Posts = GetPosts(TopicId)
                        .ToList()
                        .Skip(Math.Min((PageNum - 1) * pageSize.Value) ceva)
                        .Take(pageSize.Value).ToList();

            TopicTitle = _dbContext.PhpbbTopics.FirstOrDefault(t => t.TopicId == TopicId)?.TopicTitle ?? "untitled";

            Pagination = new _PaginationPartialModel
            {
                Link = $"/ViewTopic?TopicId={TopicId}&PageNum=1",
                Posts = _dbContext.PhpbbPosts.Count(p => p.TopicId == TopicId),
                PostsPerPage = pageSize.Value,
                CurrentPage = PageNum
            };

            return Page();
        }

        private IEnumerable<PostDisplay> GetPosts(int TopicId)
        {
            var parser = new BBCodeParser(new[]
            {
                new BBTag("b", "<b>", "</b>"),
                new BBTag("i", "<span style=\"font-style:italic;\">", "</span>"),
                new BBTag("u", "<span style=\"text-decoration:underline;\">", "</span>"),
                new BBTag("code", "<pre class=\"prettyprint\">", "</pre>"),
                new BBTag("img", "<img src=\"${content}\" />", "", false, true),
                new BBTag("quote", "<blockquote>${name}", "</blockquote>",
                    new BBAttribute("name", "", (a) => string.IsNullOrWhiteSpace(a.AttributeValue) ? "" : $"<b>{HttpUtility.HtmlDecode(a.AttributeValue).Trim('"')}</b> a scris:<br/>")),
                new BBTag("list", "<ul>", "</ul>"),
                new BBTag("*", "<li>", "</li>", true, false),
                new BBTag("url", "<a href=\"${href}\">", "</a>", new BBAttribute("href", ""), new BBAttribute("href", "href")),
                new BBTag("color", "<span style=\"color:${code}\">", "</span>", new BBAttribute("code", ""), new BBAttribute("code", "code")),
                new BBTag("youtube", "<br/><iframe width=\"560\" height=\"315\" src=\"https://www.youtube.com/embed/${content}?html5=1\" frameborder=\"0\" allowfullscreen>", "</iframe><br/>",false, true),
                new BBTag("size", "<span style=\"font-size:${fsize}\">", "</span>", new BBAttribute("fsize", ""), new BBAttribute("fsize", "fsize"))
            });


            return from p in _dbContext.PhpbbPosts
                   where p.TopicId == TopicId
                   orderby p.PostTime ascending

                   join u in _dbContext.PhpbbUsers
                   on p.PosterId equals u.UserId
                   into joined

                   from j in joined.DefaultIfEmpty()
                   select new PostDisplay
                   {
                       PostTitle = HttpUtility.HtmlDecode(p.PostSubject),
                       PostText = HttpUtility.HtmlDecode(parser.ToHtml(p.PostText.Replace($":{p.BbcodeUid}", ""))),
                       AuthorName = j.UserId == 1 ? p.PostUsername : j.Username,
                       AuthorId = j.UserId == 1 ? null as int? : j.UserId,
                       PostCreationTime = p.PostTime.TimestampToLocalTime(),
                       PostModifiedTime = p.PostEditTime.TimestampToLocalTime(),
                       Id = p.PostId
                   };
        }

        private async Task<ClaimsPrincipal> GetUser()
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
            return user;
        }
    }
}