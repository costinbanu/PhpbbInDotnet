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
    public class IndexModel : PageModel
    {
        public IEnumerable<ForumDisplay> Forums;
        public PhpbbUsers LoggedUser;

        forumContext _dbContext;
        IHttpContextAccessor _httpContext;

        public IndexModel(forumContext context, IHttpContextAccessor httpContext)
        {
            _dbContext = context;
            _httpContext = httpContext;
            LoggedUser = JsonConvert.DeserializeObject<PhpbbUsers>(_httpContext.HttpContext.Session.GetString("user") ?? "{}");
        }

        public void OnGet()
        {
            Forums = from f1 in _dbContext.PhpbbForums
                     where f1.ForumType == 0
                     let firstChildren = from f2 in _dbContext.PhpbbForums
                                         where f2.ParentId == f1.ForumId
                                         orderby f2.LeftId
                                         select new ForumDisplay()
                                         {
                                             Id = f2.ForumId,
                                             Name = HttpUtility.HtmlDecode(f2.ForumName),
                                             LastPosterName = f2.ForumLastPosterName,
                                             LastPostTime = f2.ForumLastPostTime.TimestampToLocalTime()
                                         }
                     orderby f1.LeftId
                     select new ForumDisplay()
                     {
                         Name = HttpUtility.HtmlDecode(f1.ForumName),
                         Children = firstChildren
                     };

            //Usr = _httpContext.HttpContext.Session.GetString("user");
        }


    }
}
