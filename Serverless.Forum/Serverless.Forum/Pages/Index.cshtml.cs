using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
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
    public class IndexModel : PageModel
    {
        public IEnumerable<ForumDisplay> Forums;

        forumContext _dbContext;

        public IndexModel(forumContext context)
        {
            _dbContext = context;
        }

        public async Task OnGet()
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

            Forums = from f1 in _dbContext.PhpbbForums
                     where f1.ForumType == 0
                        && user.ToLoggedUser().UserPermissions != null
                        && !user.ToLoggedUser().UserPermissions.Any(fp => fp.ForumId == f1.ForumId && fp.AuthRoleId == 16)
                     let firstChildren = from f2 in _dbContext.PhpbbForums
                                         where f2.ParentId == f1.ForumId
                                         orderby f2.LeftId

                                         join u in _dbContext.PhpbbUsers
                                         on f2.ForumLastPosterId equals u.UserId
                                         into joined

                                         from j in joined.DefaultIfEmpty()
                                         select new ForumDisplay()
                                         {
                                             Id = f2.ForumId,
                                             Name = HttpUtility.HtmlDecode(f2.ForumName),
                                             LastPosterName = f2.ForumLastPosterName,
                                             LastPosterId = j.UserId == 1 ? null as int? : j.UserId,
                                             LastPostTime = f2.ForumLastPostTime.TimestampToLocalTime()
                                         }
                     orderby f1.LeftId
                     select new ForumDisplay()
                     {
                         Name = HttpUtility.HtmlDecode(f1.ForumName),
                         Children = firstChildren
                     };
        }
    }
}
