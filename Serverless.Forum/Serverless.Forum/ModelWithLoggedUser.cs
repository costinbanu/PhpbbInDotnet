using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
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
        protected readonly forumContext _dbContext;
        protected readonly List<PhpbbAclRoles> _adminRoles;
        protected readonly List<PhpbbAclRoles> _modRoles;

        public ModelWithLoggedUser(forumContext context)
        {
            _dbContext = context;
            _adminRoles = (from r in _dbContext.PhpbbAclRoles
                           where r.RoleType == "a_"
                           select r).ToList();
            _modRoles = (from r in _dbContext.PhpbbAclRoles
                         where r.RoleType == "m_"
                         select r).ToList();
        }

        public async Task<LoggedUser> GetCurrentUser()
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
                await HttpContext.SignInAsync(
                    CookieAuthenticationDefaults.AuthenticationScheme,
                    Utils.Instance.LoggedUserFromDbUser(
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
