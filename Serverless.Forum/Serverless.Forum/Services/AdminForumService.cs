﻿using CryptSharp.Core;
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

        public async Task<(string Message, bool? IsSuccess)> ManageForumsAsync(
            int? forumId, string forumName, string forumDesc, bool? hasPassword, string forumPassword, int? parentId,
            ForumType? forumType, List<int> childrenForums, List<string> userForumPermissions, List<string> groupForumPermissions
        )
        {
            var actual = await _context.PhpbbForums.FirstOrDefaultAsync(f => f.ForumId == forumId);
            if (string.IsNullOrWhiteSpace(forumName))
            {
                return ("Numele forumului nu este valid!", false);
            }

            actual.ForumName = forumName;
            actual.ForumDesc = forumDesc;
            if (hasPassword.HasValue && !hasPassword.Value)
            {
                actual.ForumPassword = string.Empty;
            }
            if (!string.IsNullOrWhiteSpace(forumPassword))
            {
                actual.ForumPassword = Crypter.Phpass.Crypt(forumPassword, Crypter.Phpass.GenerateSalt());
            }
            actual.ParentId = parentId ?? actual.ParentId;
            actual.ForumType = forumType ?? actual.ForumType;

            var children = await (
                from f in _context.PhpbbForums
                where f.ParentId == forumId
                orderby f.LeftId
                select f
            ).ToListAsync();

            if (!children.Select(s => s.ForumId).SequenceEqual(childrenForums))
            {
                children.ForEach(c => c.LeftId = (childrenForums.IndexOf(c.ForumId) + 1) * 2);
            }

            Dictionary<int, int> translatePermissions(List<string> permissions)
                => (from fp in permissions
                    let items = fp.Split("_", StringSplitOptions.RemoveEmptyEntries)
                    let entityId = int.Parse(items[0])
                    let roleId = int.Parse(items[1])
                    where entityId > 0
                    select new { entityId, roleId }
                    ).ToDictionary(key => key.entityId, value => value.roleId);

            var rolesForAclEntity = new Dictionary<AclEntityType, Dictionary<int, int>>
            {
                { AclEntityType.User, translatePermissions(userForumPermissions) },
                { AclEntityType.Group, translatePermissions(groupForumPermissions) }
            };

            var userPermissions = await (
                from p in _context.PhpbbAclUsers
                join r in _context.PhpbbAclRoles.AsNoTracking()
                on p.AuthRoleId equals r.RoleId
                into joined
                from j in joined
                where p.ForumId == forumId
                    && rolesForAclEntity[AclEntityType.User].Keys.Contains(p.UserId)
                    && j.RoleType == "f_"
                select p
            ).ToListAsync();
            userPermissions.ForEach(p => p.AuthRoleId = rolesForAclEntity[AclEntityType.User][p.UserId]);

            var groupPermissions = await (
                from p in _context.PhpbbAclGroups
                join r in _context.PhpbbAclRoles.AsNoTracking()
                on p.AuthRoleId equals r.RoleId
                into joined
                from j in joined
                where p.ForumId == forumId
                    && rolesForAclEntity[AclEntityType.Group].Keys.Contains(p.GroupId)
                    && j.RoleType == "f_"
                select p
            ).ToListAsync();
            groupPermissions.ForEach(p => p.AuthRoleId = rolesForAclEntity[AclEntityType.Group][p.GroupId]);

            await _context.SaveChangesAsync();

            return ($"Forumul {forumName} a fost actualizat cu succes!", true);
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
                forum => new SelectListItem(forum.Name, forum.Id.ToString(), forum.Id == parentId, forum.Id == parentId || forum.Id == forumId || forum.ParentId == forumId),
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