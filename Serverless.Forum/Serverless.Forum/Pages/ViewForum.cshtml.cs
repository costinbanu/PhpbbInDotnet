using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
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
    public class ViewForumModel : PageModel
    {
        public IEnumerable<ForumDisplay> Forums;
        public IEnumerable<TopicTransport> Topics;
        public _PaginationPartialModel Pagination;
        public LoggedUser LoggedUser;

        forumContext _dbContext;
        IHttpContextAccessor _httpContext;
        public ViewForumModel(forumContext context, IHttpContextAccessor httpContext)
        {
            _dbContext = context;
            _httpContext = httpContext;
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

        public IActionResult OnGet(int ForumId)
        {
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
                                   let pageSize = LoggedUser.TopicPostsPerPage.ContainsKey(g.TopicId) ? LoggedUser.TopicPostsPerPage[g.TopicId] : 14
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