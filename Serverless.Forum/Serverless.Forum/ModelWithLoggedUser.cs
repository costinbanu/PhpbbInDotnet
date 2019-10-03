using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Serverless.Forum.Contracts;
using Serverless.Forum.forum;
using Serverless.Forum.Utilities;
using System;
using System.Threading.Tasks;

namespace Serverless.Forum
{
    public class ModelWithLoggedUser : PageModel
    {
        protected readonly forumContext _dbContext;

        public ModelWithLoggedUser(forumContext context)
        {
            _dbContext = context;
        }

        public LoggedUser CurrentUser
        {
            get
            {
                var user = User;
                if (!user.Identity.IsAuthenticated)
                {
                    user = Utils.Instance.GetAnonymousUser(_dbContext);
                    Task.WaitAll(
                        HttpContext.SignInAsync(
                            CookieAuthenticationDefaults.AuthenticationScheme,
                            user,
                            new AuthenticationProperties
                            {
                                AllowRefresh = true,
                                ExpiresUtc = DateTimeOffset.Now.AddMonths(1),
                                IsPersistent = true,
                            }
                        )
                    );
                }
                return user.ToLoggedUser();
            }
        }
    }
}
