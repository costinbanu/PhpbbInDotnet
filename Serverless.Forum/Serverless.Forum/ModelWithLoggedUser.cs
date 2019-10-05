using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
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

        public async Task<LoggedUser> GetCurrentUserAsync()
        {
            var user = User;
            if (!user.Identity.IsAuthenticated)
            {
                user = await Utils.Instance.GetAnonymousUser(_dbContext);
                await HttpContext.SignInAsync(
                    CookieAuthenticationDefaults.AuthenticationScheme,
                    user,
                    new AuthenticationProperties
                    {
                        AllowRefresh = true,
                        ExpiresUtc = DateTimeOffset.UtcNow.AddMonths(1),
                        IsPersistent = true,
                    }
                );
            }
            return user.ToLoggedUser();
        }

        public async Task ReloadCurrentUserAsync()
        {
            var current = (await GetCurrentUserAsync()).UserId;
            if (current != 1)
            {
                var result = await HttpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                await HttpContext.SignOutAsync();
                await HttpContext.SignInAsync(
                    CookieAuthenticationDefaults.AuthenticationScheme,
                    await Utils.Instance.LoggedUserFromDbUser(
                        await _dbContext.PhpbbUsers.FirstAsync(u => u.UserId == current),
                        _dbContext
                    ),
                    new AuthenticationProperties
                    {
                        AllowRefresh = true,
                        ExpiresUtc = result.Properties.ExpiresUtc,
                        IsPersistent = true,
                    }
                );
            }
        }
    }
}
