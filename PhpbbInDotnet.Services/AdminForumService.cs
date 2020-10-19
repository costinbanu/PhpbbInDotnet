using CryptSharp.Core;
using Dapper;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using PhpbbInDotnet.DTOs;
using PhpbbInDotnet.Database;
using PhpbbInDotnet.Database.Entities;
using PhpbbInDotnet.Utilities;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Web;

namespace PhpbbInDotnet.Services
{
    public class AdminForumService
    {
        private readonly ForumDbContext _context;
        private readonly ForumTreeService _forumService;
        private readonly IConfiguration _config;

        public AdminForumService(ForumDbContext context, ForumTreeService forumService, IConfiguration config)
        {
            _context = context;
            _forumService = forumService;
            _config = config;
        }

        public async Task<(string Message, bool? IsSuccess)> ManageForumsAsync(UpsertForumDto dto)
        {
            var actual = await _context.PhpbbForums.FirstOrDefaultAsync(f => f.ForumId == dto.ForumId);
            var isNewForum = false;
            if (string.IsNullOrWhiteSpace(dto.ForumName))
            {
                return ("Numele forumului nu este valid!", false);
            }
            if ((dto.ForumId ?? 0) > 0 && actual == null)
            {
                return ($"Forumul '{dto.ForumId}' nu există.", false);
            }
            else if ((dto.ForumId ?? 0) == 0)
            {
                actual = new PhpbbForums();
                isNewForum = true;
            }
            actual.ForumName = dto.ForumName;
            actual.ForumDesc = dto.ForumDesc ?? string.Empty;
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
            actual.ForumRules = HttpUtility.HtmlEncode(dto.ForumRules ?? actual.ForumRules ?? string.Empty);
            actual.ForumRulesLink = HttpUtility.HtmlEncode(dto.ForumRulesLink ?? actual.ForumRules ?? string.Empty);
            if (isNewForum)
            {
                var result = _context.PhpbbForums.Add(actual);
                result.Entity.ForumId = 0;
                await _context.SaveChangesAsync();
                actual = result.Entity;
            }
            else
            {
                await _context.SaveChangesAsync();
            }

            var children = await (
                from f in _context.PhpbbForums
                where f.ParentId == actual.ForumId
                orderby f.LeftId
                select f
            ).ToListAsync();

            if (children.Any())
            {
                children.ForEach(c => c.LeftId = ((dto.ChildrenForums?.IndexOf(c.ForumId) ?? 0) + 1) * 2);
            }

            (int entityId, int roleId) translatePermission(string permission)
            {
                if (string.IsNullOrWhiteSpace(permission))
                {
                    return (-1, -1);
                }
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
                var (entityId, roleId) = translatePermission(dto.UserForumPermissions?[idx]);
                _context.PhpbbAclUsers.Remove(await _context.PhpbbAclUsers.FirstOrDefaultAsync(x => x.UserId == entityId && x.AuthRoleId == roleId && x.ForumId == actual.ForumId));
            }

            foreach (var idx in dto.GroupPermissionToRemove ?? new List<int>())
            {
                var (entityId, roleId) = translatePermission(dto.GroupForumPermissions?[idx]);
                _context.PhpbbAclGroups.Remove(await _context.PhpbbAclGroups.FirstOrDefaultAsync(x => x.GroupId == entityId && x.AuthRoleId == roleId && x.ForumId == actual.ForumId));
            }

            var rolesForAclEntity = new Dictionary<AclEntityType, Dictionary<int, int>>
            {
                { AclEntityType.User, translatePermissions(dto.UserForumPermissions) },
                { AclEntityType.Group, translatePermissions(dto.GroupForumPermissions) }
            };

            await _context.SaveChangesAsync();

            var connection = _context.Database.GetDbConnection();
            await connection.OpenIfNeededAsync();
            var select = @"SELECT t.*
                             FROM {0} t
                             JOIN phpbb_acl_roles r ON t.auth_role_id = r.role_id
                            WHERE t.forum_id = @forumId AND t.{1} = @entityId AND r.role_type = 'f_'";
            var update = @"UPDATE {0}
                              SET auth_role_id = @roleId
                            WHERE {1} = @entityId AND forum_id = @forumId AND auth_option_id = @auth_option_id";
            var insert = @"INSERT INTO {0} ({1}, forum_id, auth_option_id, auth_role_id, auth_setting) 
                           VALUES (@entityId, @forumId, 0, @roleId, 0)";

            foreach (var byType in rolesForAclEntity)
            {
                foreach (var byId in byType.Value)
                {
                    var prefix = byType.Key == AclEntityType.Group ? "group" : "user";
                    var table = $"phpbb_acl_{prefix}s";
                    var entityIdColumn = $"{prefix}_id";
                    var entity = await connection.QueryFirstOrDefaultAsync(string.Format(select, table, entityIdColumn), new { actual.ForumId, entityId = byId.Key });
                    if (entity == null)
                    {
                        await connection.ExecuteAsync(string.Format(insert, table, entityIdColumn), new { entityId = byId.Key, actual.ForumId, roleId = byId.Value });
                    }
                    else
                    {
                        await connection.ExecuteAsync(string.Format(update, table, entityIdColumn), new { entityId = byId.Key, actual.ForumId, entity.auth_option_id, roleId = byId.Value });
                    }
                }
            }

            return ($"Forumul {actual.ForumName} a fost actualizat cu succes!", true);
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

        public async Task<List<SelectListItem>> FlatForumTreeAsListItem(int parentId, LoggedUser user)
        {
            var tree = await _forumService.GetForumTree(user, false);
            var list = new List<SelectListItem>();

            int getOrder(int forumId)
            {
                if (tree.TryGetValue(new ForumTree { ForumId = forumId }, out var forum))
                {
                    return forum.LeftId ?? 0;
                }
                return forumId;
            }

            void traverse(int cur)
            {
                if (!tree.TryGetValue(new ForumTree { ForumId = cur }, out var node))
                {
                    return;
                }

                if (tree.TryGetValue(new ForumTree { ForumId = cur }, out var forum))
                {
                    var indent = new string('-', node.Level);
                    list.Add(new SelectListItem($"{indent}{HttpUtility.HtmlDecode(forum.ForumName)}", forum.ForumId.ToString(), forum.ForumId == parentId));
                }
                else
                {
                    list.Add(new SelectListItem(_config.GetValue<string>("ForumName"), "0", parentId == 0));
                }

                foreach (var child in node.ChildrenList?.OrderBy(getOrder) ?? Enumerable.Empty<int>())
                {
                    traverse(child);
                }
            }

            traverse(0);
            return list;
        }

        public async Task<IEnumerable<ForumPermissions>> GetPermissions(int forumId)
        {
            var connection = _context.Database.GetDbConnection();
            await connection.OpenIfNeededAsync();
            return await connection.QueryAsync<ForumPermissions>("CALL `forum`.`get_forum_permissions`(@forumId);", new { forumId });
        }

        public async Task<(string Message, bool? IsSuccess)> DeleteForum( int forumId)
        {
            try
            {
                var forum = await _context.PhpbbForums.FirstOrDefaultAsync(x => x.ForumId == forumId);
                if (forum == null)
                {
                    return ($"Forumul '{forumId}' nu există.", false);
                }
                if (await _context.PhpbbForums.AsNoTracking().CountAsync(x => x.ParentId == forumId) > 0)
                {
                    return ($"Forumul '{forumId}' nu poate fi șters deoarece conține sub-forumuri.", false);
                }
                if (await _context.PhpbbTopics.AsNoTracking().CountAsync(x => x.ForumId == forumId) > 0)
                {
                    return ($"Forumul '{forumId}' nu poate fi șters deoarece conține subiecte.", false);
                }
                _context.PhpbbForums.Remove(forum);
                await _context.SaveChangesAsync();
                return ("Forumul a fost șters cu succes.", true);
            }
            catch
            {
                return ("A intervenit o eroare.", false);
            }
        }
    }
}
