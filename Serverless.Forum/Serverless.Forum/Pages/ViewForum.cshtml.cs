using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Newtonsoft.Json;
using Serverless.Forum.Contracts;
using Serverless.Forum.forum;
using Serverless.Forum.Utilities;

namespace Serverless.Forum.Pages
{
    public class ViewForumModel : PageModel
    {
        public IEnumerable<ForumDisplay> Forums;
        public IEnumerable<TopicDisplay> Topics;
        public PhpbbUsers LoggedUser;

        forumContext _dbContext;
        IHttpContextAccessor _httpContext;
        public ViewForumModel(forumContext context, IHttpContextAccessor httpContext)
        {
            _dbContext = context;
            _httpContext = httpContext;
            LoggedUser = JsonConvert.DeserializeObject<PhpbbUsers>(_httpContext.HttpContext.Session.GetString("user") ?? "{}");
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

            Topics = from t in _dbContext.PhpbbTopics
                     where t.ForumId == ForumId
                     orderby t.TopicLastPostTime descending
                     select new TopicDisplay
                     {
                         Id = t.ForumId,
                         Title = HttpUtility.HtmlDecode(t.TopicTitle),
                         LastPosterName = t.TopicLastPosterName,
                         LastPostTime = t.TopicLastPostTime.TimestampToLocalTime()
                     };
            return Page();
        }
    }
}