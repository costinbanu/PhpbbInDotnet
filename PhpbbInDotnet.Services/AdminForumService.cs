using CryptSharp.Core;
using Dapper;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using PhpbbInDotnet.Database;
using PhpbbInDotnet.Database.Entities;
using PhpbbInDotnet.Domain;
using PhpbbInDotnet.Domain.Extensions;
using PhpbbInDotnet.Domain.Utilities;
using PhpbbInDotnet.Languages;
using PhpbbInDotnet.Objects;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Web;

namespace PhpbbInDotnet.Services
{
    class AdminForumService : IAdminForumService
    {
        private readonly IForumDbContext _context;
        private readonly IForumTreeService _forumService;
        private readonly IConfiguration _config;
        private readonly IOperationLogService _operationLogService;
        private readonly ICommonUtils _utils;
        private readonly ITranslationProvider _translationProvider;

        public AdminForumService(IForumDbContext context, IForumTreeService forumService, IConfiguration config, ICommonUtils utils,
            ITranslationProvider translationProvider, IOperationLogService operationLogService)
        {
            _context = context;
            _forumService = forumService;
            _config = config;
            _operationLogService = operationLogService;
            _utils = utils;
            _translationProvider = translationProvider;
        }

        public async Task<(string Message, bool? IsSuccess)> ManageForumsAsync(UpsertForumDto dto, int adminUserId, bool isRoot)
        {
            var lang = _translationProvider.GetLanguage();
            try
            {
                if (isRoot)
                {
                    await ReorderChildren(0);
                    await _context.SaveChangesAsync();
                    return (string.Format(_translationProvider.Admin[lang, "FORUM_UPDATED_SUCCESSFULLY_FORMAT"], _config.GetObject<string>("ForumName")), true);
                }

                var actual = await _context.PhpbbForums.FirstOrDefaultAsync(f => f.ForumId == dto.ForumId);
                var isNewForum = false;
                if (string.IsNullOrWhiteSpace(dto.ForumName))
                {
                    return (_translationProvider.Admin[lang, "INVALID_FORUM_NAME"], false);
                }
                if ((dto.ForumId ?? 0) > 0 && actual == null)
                {
                    return (string.Format(_translationProvider.Admin[lang, "FORUM_DOESNT_EXIST_FORMAT"], dto.ForumId), false);
                }
                else if ((dto.ForumId ?? 0) == 0)
                {
                    actual = new PhpbbForums();
                    isNewForum = true;
                }
                actual!.ForumName = dto.ForumName;
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

                await ReorderChildren(actual.ForumId);

                var rolesForAclEntity = new Dictionary<AclEntityType, HashSet<(int entityId, int roleId)>>
                {
                    { AclEntityType.User, translatePermissions(dto.UserForumPermissions) },
                    { AclEntityType.Group, translatePermissions(dto.GroupForumPermissions) }
                };

                foreach (var idx in dto.UserPermissionToRemove.EmptyIfNull())
                {
                    var (entityId, roleId) = translatePermission(dto.UserForumPermissions?[idx]);
                    _context.PhpbbAclUsers.Remove(await _context.PhpbbAclUsers.FirstAsync(x => x.UserId == entityId && x.AuthRoleId == roleId && x.ForumId == actual.ForumId));
                    rolesForAclEntity[AclEntityType.User].Remove((entityId, roleId));
                }

                foreach (var idx in dto.GroupPermissionToRemove.EmptyIfNull())
                {
                    var (entityId, roleId) = translatePermission(dto.GroupForumPermissions?[idx]);
                    _context.PhpbbAclGroups.Remove(await _context.PhpbbAclGroups.FirstAsync(x => x.GroupId == entityId && x.AuthRoleId == roleId && x.ForumId == actual.ForumId));
                    rolesForAclEntity[AclEntityType.Group].Remove((entityId, roleId));
                }

                await _context.SaveChangesAsync();

                var tasks = new List<Task>();
                foreach (var byType in rolesForAclEntity)
                {
                    foreach (var (entityId, roleId) in byType.Value)
                    {
                        var prefix = byType.Key == AclEntityType.Group ? "group" : "user";
                        var table = $"phpbb_acl_{prefix}s";
                        var entityIdColumn = $"{prefix}_id";

                        tasks.Add(ManagePermissions(table, entityIdColumn, actual.ForumId, entityId, roleId));
                    }
                }
                await Task.WhenAll(tasks);

                await _operationLogService.LogAdminForumAction(isNewForum ? AdminForumActions.Add : AdminForumActions.Update, adminUserId, actual);

                return (string.Format(_translationProvider.Admin[lang, "FORUM_UPDATED_SUCCESSFULLY_FORMAT"], actual.ForumName), true);
            }
            catch (Exception ex)
            {
                var id = _utils.HandleError(ex);
                return (string.Format(_translationProvider.Errors[lang, "AN_ERROR_OCCURRED_TRY_AGAIN_ID_FORMAT"], id), false);
            }

            (int entityId, int roleId) translatePermission(string? permission)
            {
                if (string.IsNullOrWhiteSpace(permission))
                {
                    return (-1, -1);
                }
                var items = permission.Split('_', StringSplitOptions.RemoveEmptyEntries);
                return (int.Parse(items[0]), int.Parse(items[1]));
            }

            HashSet<(int entityId, int roleId)> translatePermissions(List<string>? permissions)
                => new(
                    from fp in permissions.EmptyIfNull()
                    let item = translatePermission(fp)
                    where item.entityId > 0
                    select item);

            async Task ReorderChildren(int forumId)
            {
                var children = await (
                    from f in _context.PhpbbForums
                    where f.ParentId == forumId
                    orderby f.LeftId
                    select f
                ).ToListAsync();

                if (children.Any())
                {
                    children.ForEach(c => c.LeftId = ((dto.ChildrenForums?.IndexOf(c.ForumId) ?? 0) + 1) * 2);
                }
            }

            async Task ManagePermissions(string table, string entityIdColumn, int forumId, int entityId, int roleId)
            {
                var sqlExecuter = _context.GetSqlExecuter();
                var existing = (await sqlExecuter.QueryAsync(
                    $@"SELECT t.*
                        FROM {table} t
                        JOIN phpbb_acl_roles r 
                          ON t.auth_role_id = r.role_id
                       WHERE t.forum_id = @forumId 
                         AND t.{entityIdColumn} = @entityId 
                         AND r.role_type = 'f_'",
                    new { forumId, entityId })).AsList();

                if (existing.Count == 1 && existing[0].auth_role_id != roleId)
                {
                    await sqlExecuter.ExecuteAsync(
                        $@"UPDATE {table}
                              SET auth_role_id = @roleId
                            WHERE {entityIdColumn} = @entityId 
                              AND forum_id = @forumId 
                              AND auth_option_id = @auth_option_id",
                        new { entityId, forumId, existing[0].auth_option_id, roleId });
                }
                else if (existing.Count != 1)
                {
                    if (existing.Count > 1)
                    {
                        await sqlExecuter.ExecuteAsync(
                            $@"DELETE FROM {table}
                                WHERE {entityIdColumn} = @entityId
                                  AND forum_id = @forumId
                                  AND auth_option_id = @auth_option_id
                                  AND auth_role_id = @auth_role_id",
                            existing.Select(e => new { entityId, forumId, e.auth_option_id, e.auth_role_id }));
                    }
                    await sqlExecuter.ExecuteAsync(
                        $@"INSERT INTO {table} ({entityIdColumn}, forum_id, auth_option_id, auth_role_id, auth_setting) 
                                VALUES (@entityId, @forumId, 0, @roleId, 0)",
                        new { entityId, forumId, roleId });
                }
            }
        }

        public async Task<(PhpbbForums Forum, List<PhpbbForums> Children)> ShowForum(int forumId)
        {
            var forumTask = _context.PhpbbForums.AsNoTracking().FirstAsync(f => f.ForumId == forumId);
            var childrenTask = (
                from f in _context.PhpbbForums
                where f.ParentId == forumId
                orderby f.LeftId
                select f
            ).ToListAsync();
            await Task.WhenAll(forumTask, childrenTask);
            return (await forumTask, await childrenTask);
        }

        public async Task<List<SelectListItem>> FlatForumTreeAsListItem(int parentId, AuthenticatedUserExpanded? user)
        {
            var tree = await _forumService.GetForumTree(user, false, false);
            var list = new List<SelectListItem>();

            traverse(0);
            return list;

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
        }

        public async Task<IEnumerable<ForumPermissions>> GetPermissions(int forumId)
            => await (_context.GetSqlExecuter()).QueryAsync<ForumPermissions>(
                sql: "CALL get_forum_permissions(@forumId);",
                param: new { forumId }
            );

        public async Task<(string Message, bool? IsSuccess)> DeleteForum(int forumId, int adminUserId)
        {
            var lang = _translationProvider.GetLanguage();
            try
            {
                var forum = await _context.PhpbbForums.FirstOrDefaultAsync(x => x.ForumId == forumId);
                if (forum == null)
                {
                    return (string.Format(_translationProvider.Admin[lang, "FORUM_DOESNT_EXIST_FORMAT"], forumId), false);
                }
                if (await _context.PhpbbForums.AsNoTracking().CountAsync(x => x.ParentId == forumId) > 0)
                {
                    return (string.Format(_translationProvider.Admin[lang, "CANT_DELETE_HAS_CHILDREN_FORMAT"], forum.ForumName), false);
                }
                if (await _context.PhpbbTopics.AsNoTracking().CountAsync(x => x.ForumId == forumId) > 0)
                {
                    return (string.Format(_translationProvider.Admin[lang, "CANT_DELETE_HAS_TOPICS_FORMAT"], forum.ForumName), false);
                }

                var dto = new ForumDto
                {
                    ForumId = forum.ForumId,
                    ForumName = forum.ForumName,
                    ForumDesc = forum.ForumDesc,
                    ForumPassword = forum.ForumPassword,
                    ParentId = forum.ParentId,
                    ForumType = forum.ForumType,
                    ForumRules = forum.ForumRules,
                    ForumRulesLink = forum.ForumRulesLink,
                    LeftId = forum.LeftId,
                    ForumLastPostId = forum.ForumLastPostId,
                    ForumLastPosterId = forum.ForumLastPosterId,
                    ForumLastPostSubject = forum.ForumLastPostSubject,
                    ForumLastPostTime = forum.ForumLastPostTime,
                    ForumLastPosterName = forum.ForumLastPosterName,
                    ForumLastPosterColour = forum.ForumLastPosterColour
                };
                await _context.PhpbbRecycleBin.AddAsync(new PhpbbRecycleBin
                {
                    Id = forum.ForumId,
                    Type = RecycleBinItemType.Forum,
                    Content = await CompressionUtility.CompressObject(dto),
                    DeleteTime = DateTime.UtcNow.ToUnixTimestamp(),
                    DeleteUser = adminUserId
                });

                _context.PhpbbForums.Remove(forum);
                await _context.SaveChangesAsync();

                await _operationLogService.LogAdminForumAction(AdminForumActions.Delete, adminUserId, forum);

                return (string.Format(_translationProvider.Admin[lang, "FORUM_DELETED_SUCCESSFULLY_FORMAT"], forum.ForumName), true);
            }
            catch (Exception ex)
            {
                var id = _utils.HandleError(ex);
                return (string.Format(_translationProvider.Errors[lang, "AN_ERROR_OCCURRED_TRY_AGAIN_ID_FORMAT"], id), false);
            }
        }
    }
}
