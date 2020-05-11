using CryptSharp.Core;
using Dapper;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Serverless.Forum.Contracts;
using Serverless.Forum.ForumDb;
using Serverless.Forum.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Serverless.Forum.Services
{
    public class AdminForumService
    {
        private readonly ForumDbContext _context;
        private readonly ForumTreeService _forumService;

        public AdminForumService(ForumDbContext context, ForumTreeService forumService)
        {
            _context = context;
            _forumService = forumService;
        }

        public async Task<(string Message, bool? IsSuccess)> ManageForumsAsync(UpsertForumDto dto)
        {
            var actual = await _context.PhpbbForums.FirstOrDefaultAsync(f => f.ForumId == dto.ForumId);
            if (string.IsNullOrWhiteSpace(dto.ForumName))
            {
                return ("Numele forumului nu este valid!", false);
            }

            actual.ForumName = dto.ForumName;
            actual.ForumDesc = dto.ForumDesc;
            if (dto.HasPassword.HasValue && !dto.HasPassword.Value)
            {
                actual.ForumPassword = string.Empty;
            }
            if (!string.IsNullOrWhiteSpace(dto.ForumPassword))
            {
                actual.ForumPassword = Crypter.Phpass.Crypt(dto.ForumPassword, Crypter.Phpass.GenerateSalt());
            }
            actual.ParentId = dto.ParentId ?? actual.ParentId;
            actual.ForumType = dto.ForumType ?? actual.ForumType;

            var children = await (
                from f in _context.PhpbbForums
                where f.ParentId == dto.ForumId
                orderby f.LeftId
                select f
            ).ToListAsync();

            if (!children.Select(s => s.ForumId).SequenceEqual(dto.ChildrenForums ?? new List<int>()))
            {
                children.ForEach(c => c.LeftId = (dto.ChildrenForums.IndexOf(c.ForumId) + 1) * 2);
            }

            (int entityId, int roleId) translatePermission(string permission)
            {
                var items = permission.Split('_', StringSplitOptions.RemoveEmptyEntries);
                return (int.Parse(items[0]), int.Parse(items[1]));
            }

            Dictionary<int, int> translatePermissions(List<string> permissions)
                => (from fp in permissions ?? new List<string>()
                    let item = translatePermission(fp)
                    where item.entityId > 0
                    select item
                    ).ToDictionary(key => key.entityId, value => value.roleId);

            foreach (var idx in dto.UserPermissionToRemove ?? new List<int>())
            {
                var (entityId, roleId) = translatePermission(dto.UserForumPermissions[idx]);
                _context.PhpbbAclUsers.Remove(await _context.PhpbbAclUsers.FirstOrDefaultAsync(x => x.UserId == entityId && x.AuthRoleId == roleId && x.ForumId == dto.ForumId));
            }

            foreach (var idx in dto.GroupPermissionToRemove ?? new List<int>())
            {
                var (entityId, roleId) = translatePermission(dto.GroupForumPermissions[idx]);
                _context.PhpbbAclGroups.Remove(await _context.PhpbbAclGroups.FirstOrDefaultAsync(x => x.GroupId == entityId && x.AuthRoleId == roleId && x.ForumId == dto.ForumId));
            }

            var rolesForAclEntity = new Dictionary<AclEntityType, Dictionary<int, int>>
            {
                { AclEntityType.User, translatePermissions(dto.UserForumPermissions) },
                { AclEntityType.Group, translatePermissions(dto.GroupForumPermissions) }
            };

            var userPermissions = await (
                from p in _context.PhpbbAclUsers
                join r in _context.PhpbbAclRoles.AsNoTracking()
                on p.AuthRoleId equals r.RoleId
                into joined
                from j in joined
                where p.ForumId == dto.ForumId
                    && rolesForAclEntity[AclEntityType.User].Keys.Contains(p.UserId)
                    && j.RoleType == "f_"
                select p
            ).ToListAsync();
            foreach (var existing in userPermissions)
            {
                existing.AuthRoleId = rolesForAclEntity[AclEntityType.User][existing.UserId];
                rolesForAclEntity[AclEntityType.User].Remove(existing.UserId);
            }
            await _context.PhpbbAclUsers.AddRangeAsync(
                rolesForAclEntity[AclEntityType.User].Select(r =>
                    new PhpbbAclUsers
                    {
                        ForumId = dto.ForumId.Value,
                        UserId = r.Key,
                        AuthRoleId = r.Value,
                        AuthOptionId = 0,
                        AuthSetting = 0
                    }
                )
            );

            var groupPermissions = await (
                from p in _context.PhpbbAclGroups
                join r in _context.PhpbbAclRoles.AsNoTracking()
                on p.AuthRoleId equals r.RoleId
                into joined
                from j in joined
                where p.ForumId == dto.ForumId
                    && rolesForAclEntity[AclEntityType.Group].Keys.Contains(p.GroupId)
                    && j.RoleType == "f_"
                select p
            ).ToListAsync();
            foreach (var p in groupPermissions)
            {
                p.AuthRoleId = rolesForAclEntity[AclEntityType.Group][p.GroupId];
                rolesForAclEntity[AclEntityType.Group].Remove(p.GroupId);
            }
            await _context.PhpbbAclGroups.AddRangeAsync(
                rolesForAclEntity[AclEntityType.Group].Select(r =>
                    new PhpbbAclGroups
                    {
                        ForumId = dto.ForumId.Value,
                        GroupId = r.Key,
                        AuthRoleId = r.Value,
                        AuthOptionId = 0,
                        AuthSetting = 0
                    }
                )
            );
            await _context.SaveChangesAsync();

            return ($"Forumul {dto.ForumName} a fost actualizat cu succes!", true);
        }

        public async Task<(PhpbbForums Forum, List<PhpbbForums> Children)> ShowForum(int forumId)
            => (
                await _context.PhpbbForums.AsNoTracking().FirstOrDefaultAsync(f => f.ForumId == forumId),
                await (
                    from f in _context.PhpbbForums
                    where f.ParentId == forumId
                    orderby f.LeftId
                    select f
                ).ToListAsync()
            );

        public async Task<List<SelectListItem>> FlatForumTreeAsListItem(int parentId, int forumId)
            => _forumService.GetPathInTree(
                await _forumService.GetForumTreeAsync(),
                forum => new SelectListItem(forum.Name, forum.Id.ToString(), forum.Id == parentId, forum.Id == forumId || forum.ParentId == forumId),
                (item, level) => item.Text = $"{new string('-', level)} {item.Text}"
            );

        public async Task<IEnumerable<ForumPermissions>> GetPermissions(int forumId)
        {
            using var connection = _context.Database.GetDbConnection();
            await connection.OpenAsync();
            DefaultTypeMap.MatchNamesWithUnderscores = true;
            return await connection.QueryAsync<ForumPermissions>("CALL `forum`.`get_forum_permissions`(@forumId);", new { forumId });
        }
    }
}
