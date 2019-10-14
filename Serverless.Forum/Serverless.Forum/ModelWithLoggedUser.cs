using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Serverless.Forum.Contracts;
using Serverless.Forum.forum;
using Serverless.Forum.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Serverless.Forum
{
    public class ModelWithLoggedUser : PageModel
    {
        protected readonly List<PhpbbAclRoles> _adminRoles;
        protected readonly List<PhpbbAclRoles> _modRoles;
        protected readonly IConfiguration _config;

        public ModelWithLoggedUser(IConfiguration config)
        {
            _config = config;
            using (var context = new forumContext(config))
            {
                _adminRoles = (from r in context.PhpbbAclRoles
                               where r.RoleType == "a_"
                               select r).ToList();
                _modRoles = (from r in context.PhpbbAclRoles
                             where r.RoleType == "m_"
                             select r).ToList();
            }
        }

        public async Task<LoggedUser> GetCurrentUser()
        {
            var user = User;
            if (!user.Identity.IsAuthenticated)
            {
                using (var context = new forumContext(_config))
                {
                    user = await Utils.Instance.GetAnonymousUser(context);
                }
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
            return await user.ToLoggedUser();
        }

        public async Task<bool> IsCurrentUserAdminHere(int forumId)
        {
            return (from up in (await GetCurrentUser()).UserPermissions
                    where up.ForumId == forumId || up.ForumId == 0
                    join a in _adminRoles
                    on up.AuthRoleId equals a.RoleId
                    select up).Any();
        }

        public async Task<bool> IsCurrentUserModHere(int forumId)
        {
            return (from up in (await GetCurrentUser()).UserPermissions
                    where up.ForumId == forumId || up.ForumId == 0
                    join a in _modRoles
                    on up.AuthRoleId equals a.RoleId
                    select up).Any();
        }

        public async Task ReloadCurrentUser()
        {
            var current = (await GetCurrentUser()).UserId;
            if (current != 1)
            {
                var result = await HttpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                await HttpContext.SignOutAsync();
                using (var context = new forumContext(_config))
                {
                    await HttpContext.SignInAsync(
                        CookieAuthenticationDefaults.AuthenticationScheme,
                        await Utils.Instance.LoggedUserFromDbUser(
                            await context.PhpbbUsers.FirstAsync(u => u.UserId == current),
                            context
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
}
