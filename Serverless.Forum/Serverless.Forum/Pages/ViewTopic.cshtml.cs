using CodeKicker.BBCode;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Newtonsoft.Json;
using Serverless.Forum.Contracts;
using Serverless.Forum.forum;
using Serverless.Forum.Utilities;
using System.Collections.Generic;
using System.Linq;
using System.Web;

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
            if (_httpContext.HttpContext.Session.GetString("user") == null)
            {
                LoggedUser = Acl.Instance.GetAnonymousUser(_dbContext);
                _httpContext.HttpContext.Session.SetString("user", JsonConvert.SerializeObject(LoggedUser));
            }
            else
            {
                LoggedUser = JsonConvert.DeserializeObject<LoggedUser>(_httpContext.HttpContext.Session.GetString("user"));
            }
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
                new BBTag("quote", "<blockquote>${name}", "</blockquote>",
                    new BBAttribute("name", "", (a) => string.IsNullOrWhiteSpace(a.AttributeValue) ? "" : $"<b>{HttpUtility.HtmlDecode(a.AttributeValue).Trim('"')}</b> a scris:<br/>")),
                new BBTag("list", "<ul>", "</ul>"),
                new BBTag("*", "<li>", "</li>", true, false),
                new BBTag("url", "<a href=\"${href}\">", "</a>", new BBAttribute("href", ""), new BBAttribute("href", "href")),
                new BBTag("color", "<span style=\"color:${code}\">", "</span>", new BBAttribute("code", ""), new BBAttribute("code", "code")),
                new BBTag("youtube", "<br/><iframe width=\"560\" height=\"315\" src=\"https://www.youtube.com/embed/${content}?html5=1\" frameborder=\"0\" allowfullscreen>", "</iframe><br/>",false, true),
                new BBTag("size", "<span style=\"font-size:${fsize}\">", "</span>", new BBAttribute("fsize", ""), new BBAttribute("fsize", "fsize"))
            });


            Posts = (from p in _dbContext.PhpbbPosts
                     where p.TopicId == TopicId
                     orderby p.PostTime ascending

                     join u in _dbContext.PhpbbUsers
                     on p.PosterId equals u.UserId
                     into joined

                     from j in joined
                     select new PostDisplay
                     {
                         PostText = HttpUtility.HtmlDecode(parser.ToHtml(p.PostText.Replace($":{p.BbcodeUid}", ""))),
                         AuthorName = j.UserId == 1 ? p.PostUsername : j.Username,
                         AuthorId = j.UserId == 1 ? null as int? : j.UserId,
                         PostCreationTime = p.PostTime.TimestampToLocalTime(),
                         PostModifiedTime = p.PostEditTime.TimestampToLocalTime(),
                         Id = p.PostId
                     })
                    .ToList().Skip((PageNum - 1) * pageSize).Take(pageSize).ToList();

            TopicTitle = _dbContext.PhpbbTopics.FirstOrDefault(t => t.TopicId == TopicId)?.TopicTitle ?? "untitled";

            Pagination = new _PaginationPartialModel
            {
                Link = $"/ViewTopic?TopicId={TopicId}&PageNum=1",
                Posts = _dbContext.PhpbbPosts.Count(p => p.TopicId == TopicId),
                PostsPerPage = pageSize,
                CurrentPage = PageNum
            };
        }
    }
}