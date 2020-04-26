using Dapper;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Serverless.Forum.Contracts;
using Serverless.Forum.ForumDb;
using Serverless.Forum.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace Serverless.Forum.Services
{
    public class UserService
    {
        private readonly IConfiguration _config;
        private readonly Utils _utils;
        private readonly ForumDbContext _context;
        private List<PhpbbAclRoles> _adminRoles;
        private List<PhpbbAclRoles> _modRoles;
        private List<PhpbbAclRoles> _userRoles;
        private PhpbbUsers _anonymousDbUser;
        private ClaimsPrincipal _anonymousClaimsPrincipal;
        private LoggedUser _anonymousLoggedUser;

        public UserService(IConfiguration config, Utils utils, ForumDbContext context)
        {
            _config = config;
            _utils = utils;
            _context = context;
        }

        public async Task<bool> IsUserAdminInForum(LoggedUser user, int forumId)
            => user != null && (
                from up in user.UserPermissions
                where up.ForumId == forumId || up.ForumId == 0
                join a in await GetAdminRolesLazy()
                on up.AuthRoleId equals a.RoleId
                select up
            ).Any();

        public async Task<bool> IsUserModeratorInForum(LoggedUser user, int forumId)
            => (await IsUserAdminInForum(user, forumId)) || (
                user != null && (
                    from up in user.UserPermissions
                    where up.ForumId == forumId || up.ForumId == 0
                    join a in await GetModRolesLazy()
                    on up.AuthRoleId equals a.RoleId
                    select up
                ).Any()
            );

        public async Task<int?> GetUserRole(LoggedUser user)
            => (from up in user.UserPermissions
                join a in await GetUserRolesLazy()
                on up.AuthRoleId equals a.RoleId
                select up.AuthRoleId as int?).FirstOrDefault();

        public async Task<PhpbbUsers> GetAnonymousDbUserAsync()
        {
            if (_anonymousDbUser != null)
            {
                return _anonymousDbUser;
            }

            _anonymousDbUser = await _context.PhpbbUsers.AsNoTracking().FirstAsync(u => u.UserId == 1);
            return _anonymousDbUser;
        }

        public async Task<ClaimsPrincipal> GetAnonymousClaimsPrincipalAsync()
        {
            if (_anonymousClaimsPrincipal != null)
            {
                return _anonymousClaimsPrincipal;
            }

            _anonymousClaimsPrincipal = await DbUserToClaimsPrincipalAsync(await GetAnonymousDbUserAsync());
            return _anonymousClaimsPrincipal;
        }

        public async Task<LoggedUser> GetAnonymousLoggedUserAsync()
        {
            if (_anonymousLoggedUser != null)
            {
                return _anonymousLoggedUser;
            }

            _anonymousLoggedUser = await ClaimsPrincipalToLoggedUserAsync(await GetAnonymousClaimsPrincipalAsync());
            return _anonymousLoggedUser;
        }

        public async Task<ClaimsPrincipal> DbUserToClaimsPrincipalAsync(PhpbbUsers user)
        {
            using var connection = _context.Database.GetDbConnection();
            await connection.OpenAsync();
            DefaultTypeMap.MatchNamesWithUnderscores = true;

            using var multi = await connection.QueryMultipleAsync("CALL `forum`.`get_user_details`(@UserId);", new { user.UserId });
            var intermediary = new LoggedUser
            {
                UserId = user.UserId,
                Username = user.Username,
                UsernameClean = user.UsernameClean,
                UserPermissions = await multi.ReadAsync<LoggedUser.Permissions>(),
                Groups = (await multi.ReadAsync<uint>()).Select(x => checked((int)x)),
                TopicPostsPerPage = (await multi.ReadAsync()).ToDictionary(key => checked((int)key.topic_id), value => checked((int)value.post_no)),
                UserDateFormat = user.UserDateformat,
                UserColor = user.UserColour,
                PostEditTime = user.UserEditTime
            };

            var identity = new ClaimsIdentity(CookieAuthenticationDefaults.AuthenticationScheme);
            identity.AddClaim(new Claim(ClaimTypes.UserData, Convert.ToBase64String(await _utils.CompressObject(intermediary))));
            return new ClaimsPrincipal(identity);
        }

        public async Task<LoggedUser> ClaimsPrincipalToLoggedUserAsync(ClaimsPrincipal principal)
            => await _utils.DecompressObject<LoggedUser>(Convert.FromBase64String(principal.Claims.FirstOrDefault()?.Value ?? string.Empty));

        public async Task<LoggedUser> DbUserToLoggedUserAsync(PhpbbUsers dbUser)
            => await ClaimsPrincipalToLoggedUserAsync(await DbUserToClaimsPrincipalAsync(dbUser));

        public async Task<List<PhpbbAclRoles>> GetUserRolesListAsync()
            => await GetUserRolesLazy();

        public async Task<List<PhpbbRanks>> GetRankListAsync()
        {
            return await _context.PhpbbRanks.AsNoTracking().ToListAsync();
        }

        public async Task<List<PhpbbGroups>> GetGroupListAsync()
        {
            return await _context.PhpbbGroups.AsNoTracking().ToListAsync();
        }

        public async Task<int?> GetUserGroupAsync(int userId)
        {
            return (await _context.PhpbbUserGroup.AsNoTracking().FirstOrDefaultAsync(x => x.UserId == userId))?.GroupId;
        }

        private async Task<List<PhpbbAclRoles>> GetUserRolesLazy()
        {
            if (_userRoles != null)
            {
                return _userRoles;
            }

            _userRoles = await (
                from r in _context.PhpbbAclRoles.AsNoTracking()
                where r.RoleType == "u_"
                select r
            ).ToListAsync();
            return _userRoles;
        }

        private async Task<List<PhpbbAclRoles>> GetModRolesLazy()
        {
            if (_modRoles != null)
            {
                return _modRoles;
            }

            _modRoles = await (
                from r in _context.PhpbbAclRoles.AsNoTracking()
                where r.RoleType == "m_"
                select r
            ).ToListAsync();
            return _modRoles;
        }

        private async Task<List<PhpbbAclRoles>> GetAdminRolesLazy()
        {
            if (_adminRoles != null)
            {
                return _adminRoles;
            }

            _adminRoles = await (
                from r in _context.PhpbbAclRoles.AsNoTracking()
                where r.RoleType == "a_"
                select r
            ).ToListAsync();
            return _adminRoles;
        }
    }
}
