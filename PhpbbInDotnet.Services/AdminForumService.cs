using CryptSharp.Core;
using Dapper;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Configuration;
using PhpbbInDotnet.Database.Entities;
using PhpbbInDotnet.Database.SqlExecuter;
using PhpbbInDotnet.Domain;
using PhpbbInDotnet.Domain.Extensions;
using PhpbbInDotnet.Domain.Utilities;
using PhpbbInDotnet.Languages;
using PhpbbInDotnet.Objects;
using Serilog;
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
        private readonly ISqlExecuter _sqlExecuter;
        private readonly IForumTreeService _forumService;
        private readonly IConfiguration _config;
        private readonly IOperationLogService _operationLogService;
        private readonly ITranslationProvider _translationProvider;
        private readonly ILogger _logger;

        public AdminForumService(ISqlExecuter sqlExecuter, IForumTreeService forumService, IConfiguration config,
            ITranslationProvider translationProvider, IOperationLogService operationLogService, ILogger logger)
        {
            _sqlExecuter = sqlExecuter;
            _forumService = forumService;
            _config = config;
            _operationLogService = operationLogService;
            _translationProvider = translationProvider;
            _logger = logger;
        }

        public async Task<UpsertForumResult> ManageForumsAsync(UpsertForumDto dto, int adminUserId, bool isRoot)
        {
            var lang = _translationProvider.GetLanguage();
            try
            {
                if (isRoot)
                {
                    await ReorderChildren(0);
                    return new(
                        isSuccess: true, 
                        message: string.Format(_translationProvider.Admin[lang, "FORUM_UPDATED_SUCCESSFULLY_FORMAT"], _config.GetObject<string>("ForumName")));
                }

                var actual = await _sqlExecuter.QueryFirstOrDefaultAsync<PhpbbForums>(
                    "SELECT * FROM phpbb_forums WHERE forum_id = @forumId",
                    new
                    {
                        dto.ForumId
                    });
                var isNewForum = false;
                if (string.IsNullOrWhiteSpace(dto.ForumName))
                {
                    return new(
                        isSuccess: false, 
                        message: _translationProvider.Admin[lang, "INVALID_FORUM_NAME"]);
                }
                if (dto.ForumId > 0 && actual == null)
                {
                    return new(
                        isSuccess: false,
                        message: string.Format(_translationProvider.Admin[lang, "FORUM_DOESNT_EXIST_FORMAT"], dto.ForumId));
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
                    actual = await _sqlExecuter.QueryFirstOrDefaultAsync<PhpbbForums>(
						@$"INSERT INTO phpbb_forums 
                                 VALUES (@ParentId, @LeftId, @RightId, @ForumParents, @ForumName, @ForumDesc, @ForumDescBitfield, @ForumDescOptions, @ForumDescUid, @ForumLink, @ForumPassword, @ForumStyle, @ForumImage, @ForumRules, @ForumRulesLink, @ForumRulesBitfield, @ForumRulesOptions, @ForumRulesUid, @ForumTopicsPerPage, @ForumType, @ForumStatus, @ForumPosts, @ForumTopics, @ForumTopicsReal, @ForumLastPostId, @ForumLastPosterId, @ForumLastPostSubject, @ForumLastPostTime, @ForumLastPosterName, @ForumLastPosterColour, @ForumFlags, @ForumOptions, @DisplaySubforumList, @DisplayOnIndex, @EnableIndexing, @EnableIcons, @EnablePrune, @PruneNext, @PruneDays, @PruneViewed, @PruneFreq, @ForumEditTime);
                          SELECT * 
                            FROM phpbb_forums 
                           WHERE forum_id = {_sqlExecuter.LastInsertedItemId}",
                        actual);
                }
                else
                {
                    await _sqlExecuter.ExecuteAsync(
                        @"UPDATE phpbb_forums
                            SET parent_id  = @ParentId,
                                left_id = @LeftId,
                                right_id  = @RightId,
                                forum_parents = @ForumParents,
                                forum_name  = @ForumName,
                                forum_desc  = @ForumDesc,
                                forum_desc_bitfield  = @ForumDescBitfield,
                                forum_desc_options  = @ForumDescOptions,
                                forum_desc_uid  = @ForumDescUid,
                                forum_link  = @ForumLink,
                                forum_password  = @ForumPassword,
                                forum_style  = @ForumStyle,
                                forum_image  = @ForumImage,
                                forum_rules  = @ForumRules,
                                forum_rules_link  = @ForumRulesLink,
                                forum_rules_bitfield  = @ForumRulesBitfield,
                                forum_rules_options  = @ForumRulesOptions,
                                forum_rules_uid  = @ForumRulesUid,
                                forum_topics_per_page  = @ForumTopicsPerPage,
                                forum_type  = @ForumType,
                                forum_status  = @ForumStatus,
                                forum_posts  = @ForumPosts,
                                forum_topics  = @ForumTopics,
                                forum_topics_real  = @ForumTopicsReal,
                                forum_last_post_id  = @ForumLastPostId,
                                forum_last_poster_id = @ForumLastPosterId,
                                forum_last_post_subject  = @ForumLastPostSubject,
                                forum_last_post_time  = @ForumLastPostTime,
                                forum_last_poster_name  = @ForumLastPosterName,
                                forum_last_poster_colour = @ForumLastPosterColour,
                                forum_flags  = @ForumFlags,
                                forum_options  = @ForumOptions,
                                display_subforum_list  = @DisplaySubforumList,
                                display_on_index  = @DisplayOnIndex,
                                enable_indexing  = @EnableIndexing,
                                enable_icons  = @EnableIcons,
                                enable_prune  = @EnablePrune,
                                prune_next  = @PruneNext,
                                prune_days  = @PruneDays,
                                prune_viewed  = @PruneViewed,
                                prune_freq  = @PruneFreq,
                                forum_edit_time  = @ForumEditTime
                            WHERE forum_id = @ForumId",
                        actual);
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
                    await _sqlExecuter.ExecuteAsync(
                        "DELETE FROM phpbb_acl_users WHERE user_id = @entityId AND auth_role_id = @roleId AND forum_id = @forumId",
                        new
                        {
                            entityId,
                            roleId,
                            actual.ForumId
                        });
                    rolesForAclEntity[AclEntityType.User].Remove((entityId, roleId));
                }

                foreach (var idx in dto.GroupPermissionToRemove.EmptyIfNull())
                {
                    var (entityId, roleId) = translatePermission(dto.GroupForumPermissions?[idx]);
					await _sqlExecuter.ExecuteAsync(
	                    "DELETE FROM phpbb_acl_groups WHERE group_id = @entityId AND auth_role_id = @roleId AND forum_id = @forumId",
	                    new
	                    {
		                    entityId,
		                    roleId,
		                    actual.ForumId
	                    });
                    rolesForAclEntity[AclEntityType.Group].Remove((entityId, roleId));
                }

                foreach (var byType in rolesForAclEntity)
                {
                    foreach (var (entityId, roleId) in byType.Value)
                    {
                        var prefix = byType.Key == AclEntityType.Group ? "group" : "user";
                        var table = $"phpbb_acl_{prefix}s";
                        var entityIdColumn = $"{prefix}_id";

                        await ManagePermissions(table, entityIdColumn, actual.ForumId, entityId, roleId);
                    }
                }

                await _operationLogService.LogAdminForumAction(isNewForum ? AdminForumActions.Add : AdminForumActions.Update, adminUserId, actual);

                return new(
                    isSuccess: true,
                    message: string.Format(_translationProvider.Admin[lang, "FORUM_UPDATED_SUCCESSFULLY_FORMAT"], actual.ForumName))
                {
                    Forum = actual
                };
            }
            catch (Exception ex)
            {
                var id = _logger.ErrorWithId(ex);
                return new (
                    isSuccess: false,
                    message: string.Format(_translationProvider.Errors[lang, "AN_ERROR_OCCURRED_TRY_AGAIN_ID_FORMAT"], id));
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
                var children = await _sqlExecuter.QueryAsync<PhpbbForums>(
                    "SELECT * FROM phpbb_forums WHERE parent_id = @forumId ORDER BY left_id", 
                    new 
                    { 
                        forumId 
                    });
                await _sqlExecuter.ExecuteAsync(
                    "UPDATE phpbb_forums SET left_id = @newLeftId WHERE forum_id = @forumId",
                    children.Select(c => new
                    {
                        newLeftId = ((dto.ChildrenForums?.IndexOf(c.ForumId) ?? 0) + 1) * 2,
                        c.ForumId
                    }));
            }

            async Task ManagePermissions(string table, string entityIdColumn, int forumId, int entityId, int roleId)
            {
                var existing = (await _sqlExecuter.QueryAsync(
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
                    await _sqlExecuter.ExecuteAsync(
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
                        await _sqlExecuter.ExecuteAsync(
                            $@"DELETE FROM {table}
                                WHERE {entityIdColumn} = @entityId
                                  AND forum_id = @forumId
                                  AND auth_option_id = @auth_option_id
                                  AND auth_role_id = @auth_role_id",
                            existing.Select(e => new { entityId, forumId, e.auth_option_id, e.auth_role_id }));
                    }
                    await _sqlExecuter.ExecuteAsync(
                        $@"INSERT INTO {table} ({entityIdColumn}, forum_id, auth_option_id, auth_role_id, auth_setting) 
                                VALUES (@entityId, @forumId, 0, @roleId, 0)",
                        new { entityId, forumId, roleId });
                }
            }
        }

        public async Task<(PhpbbForums Forum, List<PhpbbForums> Children)> ShowForum(int forumId)
        {
            var forum = await _sqlExecuter.QueryFirstOrDefaultAsync<PhpbbForums>(
                "SELECT * FROM phpbb_forums WHERE forum_id = @forumId",
                new { forumId });
            var children = await _sqlExecuter.QueryAsync<PhpbbForums>(
                "SELECT * FROM phpbb_forums WHERE parent_id = @forumId ORDER BY left_id",
                new { forumId });
            return (forum, children.AsList());
        }

        public async Task<List<SelectListItem>> FlatForumTreeAsListItem(int parentId, ForumUserExpanded? user)
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

        public Task<IEnumerable<ForumPermissions>> GetPermissions(int forumId)
            => _sqlExecuter.CallStoredProcedureAsync<ForumPermissions>("get_forum_permissions", new { forumId });

        public async Task<(string Message, bool? IsSuccess)> DeleteForum(int forumId, int adminUserId)
        {
            var lang = _translationProvider.GetLanguage();
            try
            {
                var forum = await _sqlExecuter.QueryFirstOrDefaultAsync<PhpbbForums>(
				    "SELECT * FROM phpbb_forums WHERE forum_id = @forumId",
				    new { forumId });
                if (forum == null)
                {
                    return (string.Format(_translationProvider.Admin[lang, "FORUM_DOESNT_EXIST_FORMAT"], forumId), false);
                }
                var childrenCount = await _sqlExecuter.ExecuteScalarAsync<long>(
                    "SELECT count(1) FROM phpbb_forums WHERE parent_id = @forumId",
                    new { forumId });
				if (childrenCount > 0)
                {
                    return (string.Format(_translationProvider.Admin[lang, "CANT_DELETE_HAS_CHILDREN_FORMAT"], forum.ForumName), false);
                }
                var topicCount = await _sqlExecuter.ExecuteScalarAsync<long>(
					"SELECT count(1) FROM phpbb_topics WHERE forum_id = 14",
					new { forumId });
				if (topicCount > 0)
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

                await _sqlExecuter.ExecuteAsync(
                    "INSERT INTO phpbb_recycle_bin VALUES (@id, @type, @content, @deleteTime, @deleteUser)",
                    new
                    {
                        id = forum.ForumId,
                        type = RecycleBinItemType.Forum,
                        content = await CompressionUtility.CompressObject(dto),
                        deleteTime = DateTime.UtcNow.ToUnixTimestamp(),
                        deleteUser = adminUserId
                    });

                await _sqlExecuter.ExecuteAsync(
                    "DELETE FROM phpbb_forums WHERE forum_id = @forumId",
                    new { forum.ForumId });

                await _operationLogService.LogAdminForumAction(AdminForumActions.Delete, adminUserId, forum);

                return (string.Format(_translationProvider.Admin[lang, "FORUM_DELETED_SUCCESSFULLY_FORMAT"], forum.ForumName), true);
            }
            catch (Exception ex)
            {
                var id = _logger.ErrorWithId(ex);
                return (string.Format(_translationProvider.Errors[lang, "AN_ERROR_OCCURRED_TRY_AGAIN_ID_FORMAT"], id), false);
            }
        }
    }
}
