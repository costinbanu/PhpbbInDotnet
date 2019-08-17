using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using CodeKicker.BBCode;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Newtonsoft.Json;
using Serverless.Forum.Contracts;
using Serverless.Forum.forum;
using Serverless.Forum.Utilities;

using System.Text;
using System.Text.RegularExpressions;

namespace Serverless.Forum.Pages
{
    public class ViewTopicModel : PageModel
    {
        public IEnumerable<PostDisplay> Posts;
        public string TopicTitle;
        public LoggedUser LoggedUser;
        public _PaginationPartialModel Pagination;

        forumContext _dbContext;
        IHttpContextAccessor _httpContext;
        public ViewTopicModel(forumContext context, IHttpContextAccessor httpContext)
        {
            _dbContext = context;
            _httpContext = httpContext;
            LoggedUser = JsonConvert.DeserializeObject<LoggedUser>(_httpContext.HttpContext.Session.GetString("user") ?? "{}");
        }
        public void OnGet(int TopicId, int PageNum)
        {
            var pageSize = LoggedUser.TopicPostsPerPage.ContainsKey(TopicId) ? LoggedUser.TopicPostsPerPage[TopicId] : 14;
            //var customBbCodes = from c in _dbContext.PhpbbBbcodes
            //                    select 

            var parser = new BBCodeParser(new[]
                {
                    new BBTag("b", "<b>", "</b>"),
                    new BBTag("i", "<span style=\"font-style:italic;\">", "</span>"),
                    new BBTag("u", "<span style=\"text-decoration:underline;\">", "</span>"),
                    new BBTag("code", "<pre class=\"prettyprint\">", "</pre>"),
                    new BBTag("img", "<img src=\"${content}\" />", "", false, true),
                    new BBTag("quote", "<blockquote>", "</blockquote>"),
                    new BBTag("list", "<ul>", "</ul>"),
                    new BBTag("*", "<li>", "</li>", true, false),
                    new BBTag("url", "<a href=\"${href}\">", "</a>", new BBAttribute("href", ""), new BBAttribute("href", "href")),
                });


            Posts = (from p in _dbContext.PhpbbPosts
                     where p.TopicId == TopicId
                     orderby p.PostTime ascending

                     join u in _dbContext.PhpbbUsers
                     on p.PosterId equals u.UserId
                     into joined

                     from j in joined.DefaultIfEmpty()
                     select new PostDisplay
                     {
                         PostText = parser.ToHtml(HttpUtility.HtmlDecode(p.PostText.Replace($":{p.BbcodeUid}", ""))),
                         AuthorName = j.Username ?? p.PostUsername,
                         AuthorId = j.UserId,
                         PostCreationTime = p.PostTime.TimestampToLocalTime(),
                         PostModifiedTime = p.PostEditTime.TimestampToLocalTime()
                     })
                    .ToList().Skip((PageNum - 1) * pageSize).Take(pageSize).ToList();

            TopicTitle = _dbContext.PhpbbTopics.FirstOrDefault(t => t.TopicId == TopicId)?.TopicTitle ?? "untitled";

            Pagination = new _PaginationPartialModel
            {
                Link = $"/ViewTopic?TopicId={TopicId}&PageNum=1",
                Posts = _dbContext.PhpbbPosts.Count(p => p.TopicId == TopicId),
                PostsPerPage = pageSize
            };
        }
    }
}