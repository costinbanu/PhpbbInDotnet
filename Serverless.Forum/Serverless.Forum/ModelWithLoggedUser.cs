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
        protected readonly Lazy<List<PhpbbAclRoles>> _adminRoles;
        protected readonly Lazy<List<PhpbbAclRoles>> _modRoles;
        protected readonly IConfiguration _config;
        protected readonly Utils _utils;

        private readonly Lazy<int?> _currentUserId;

        public ModelWithLoggedUser(IConfiguration config, Utils utils)
        {
            _config = config;
            _utils = utils;

            _adminRoles = new Lazy<List<PhpbbAclRoles>>(() =>
            {
                using (var context = new forumContext(config))
                {
                    return (from r in context.PhpbbAclRoles
                            where r.RoleType == "a_"
                            select r).ToList();
                }
            });

            _modRoles = new Lazy<List<PhpbbAclRoles>>(() =>
            {
                using (var context = new forumContext(config))
                {
                    return (from r in context.PhpbbAclRoles
                            where r.RoleType == "m_"
                            select r).ToList();
                }
            });

            _currentUserId = new Lazy<int?>(() => GetCurrentUserAsync().RunSync().UserId);
        }

        public async Task<LoggedUser> GetCurrentUserAsync()
        {
            var user = User;
            if (!user.Identity.IsAuthenticated)
            {
                using (var context = new forumContext(_config))
                {
                    user = _utils.Anonymous;
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
            return await user.ToLoggedUser(_utils);
        }

        public async Task<bool> IsCurrentUserAdminHere(int forumId)
        {
            return (from up in (await GetCurrentUserAsync()).UserPermissions
                    where up.ForumId == forumId || up.ForumId == 0
                    join a in _adminRoles.Value
                    on up.AuthRoleId equals a.RoleId
                    select up).Any();
        }

        public async Task<bool> IsCurrentUserModHere(int forumId)
        {
            return (from up in (await GetCurrentUserAsync()).UserPermissions
                    where up.ForumId == forumId || up.ForumId == 0
                    join a in _modRoles.Value
                    on up.AuthRoleId equals a.RoleId
                    select up).Any();
        }

        public async Task ReloadCurrentUser()
        {
            var current = (await GetCurrentUserAsync()).UserId;
            if (current != 1)
            {
                var result = await HttpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                await HttpContext.SignOutAsync();
                using (var context = new forumContext(_config))
                {
                    await HttpContext.SignInAsync(
                        CookieAuthenticationDefaults.AuthenticationScheme,
                        await _utils.LoggedUserFromDbUserAsync(
                            await context.PhpbbUsers.FirstAsync(u => u.UserId == current)
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

        public int? CurrentUserId => _currentUserId.Value;
    }
}
