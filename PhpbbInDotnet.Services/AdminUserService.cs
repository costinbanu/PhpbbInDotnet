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
using PhpbbInDotnet.Objects.EmailDtos;
using Serilog;
using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PhpbbInDotnet.Services
{
    class AdminUserService : IAdminUserService
    {
        private readonly ISqlExecuter _sqlExecuter;
        private readonly IConfiguration _config;
        private readonly IModeratorService _moderatorService;
        private readonly IOperationLogService _operationLogService;
        private readonly ITranslationProvider _translationProvider;
        private readonly ILogger _logger;
        private readonly IEmailService _emailService;

        public AdminUserService(ISqlExecuter sqlExecuter, IConfiguration config, IModeratorService moderatorService,
            ITranslationProvider translationProvider, IOperationLogService operationLogService, ILogger logger, IEmailService emailService)
        {
            _sqlExecuter = sqlExecuter;
            _config = config;
            _moderatorService = moderatorService;
            _operationLogService = operationLogService;
            _translationProvider = translationProvider;
            _logger = logger;
            _emailService = emailService;
        }

        #region User

        public async Task<List<PhpbbUsers>> GetInactiveUsers()
        {
            var toReturn = await _sqlExecuter.QueryAsync<PhpbbUsers>(
                @"SELECT * 
                   FROM phpbb_users 
                  WHERE user_inactive_time > 0 
                    AND user_inactive_reason <> @notInactive 
                    AND user_inactive_reason <> @activeNotConfirmed 
                  ORDER BY user_inactive_time DESC",
                new
                {
                    notInactive = UserInactiveReason.NotInactive,
                    activeNotConfirmed = UserInactiveReason.Active_NotConfirmed
                });
            return toReturn.AsList();
        }

        public async Task<List<PhpbbUsers>> GetActiveUsersWithUnconfirmedEmail()
        {
			var toReturn = await _sqlExecuter.QueryAsync<PhpbbUsers>(
		        @"SELECT * 
                    FROM phpbb_users 
                   WHERE user_inactive_reason = @activeNotConfirmed 
                   ORDER BY username",
		        new
		        {
			        notInactive = UserInactiveReason.NotInactive,
			        activeNotConfirmed = UserInactiveReason.Active_NotConfirmed
		        });
			return toReturn.AsList();
		}

        public async Task<(string Message, bool? IsSuccess)> DeleteUsersWithEmailNotConfirmed(int[] userIds, int adminUserId)
        {
            var lang = _translationProvider.GetLanguage();
            if (!(userIds?.Any() ?? false))
            {
                return (_translationProvider.Admin[lang, "NO_USER_SELECTED"], null);
            }

            async Task Log(IEnumerable<PhpbbUsers> users)
            {
                foreach (var user in users)
                {
                    await _operationLogService.LogAdminUserAction(AdminUserActions.Delete_KeepMessages, adminUserId, user, "Batch removing inactive users with unconfirmed email.");
                }
            }

            try
            {
                var users = await _sqlExecuter.QueryAsync<PhpbbUsers>(
                    "SELECT * FROM phpbb_users WHERE user_id IN @userIds AND user_inactive_reason = @newlyRegisteredNotConfirmed",
                    new
                    {
                        userIds = userIds.DefaultIfNullOrEmpty(),
                        newlyRegisteredNotConfirmed = UserInactiveReason.NewlyRegisteredNotConfirmed
                    });
                await _sqlExecuter.ExecuteAsync(
                    "DELETE FROM phpbb_users WHERE user_id IN @userIds",
                    new
                    {
                        userIds = users.Select(u => u.UserId).DefaultIfEmpty()
                    });

                if (users.Count() == userIds.Length)
                {
                    await Log(users);
                    return (_translationProvider.Admin[lang, "USERS_DELETED_SUCCESSFULLY"], true);
                }

                var dbUserIds = users.Select(u => u.UserId).ToList();
                var changedStatus = userIds.Where(u => !dbUserIds.Contains(u));

                await Log(users.Where(u => dbUserIds.Contains(u.UserId)));

                return (
                    string.Format(
                        _translationProvider.Admin[lang, "USERS_DELETED_PARTIALLY_FORMAT"],
                        string.Join(", ", dbUserIds),
                        _translationProvider.Enums[lang, UserInactiveReason.NewlyRegisteredNotConfirmed],
                        string.Join(", ", changedStatus)
                    ),
                    null
                );
            }
            catch (Exception ex)
            {
                var id = _logger.ErrorWithId(ex);
                return (string.Format(_translationProvider.Errors[lang, "AN_ERROR_OCCURRED_TRY_AGAIN_ID_FORMAT"], id), false);
            }
        }

        public async Task<(string Message, bool? IsSuccess)> ManageUser(AdminUserActions? action, int? userId, int adminUserId)
        {
            var lang = _translationProvider.GetLanguage();
            if (userId == Constants.ANONYMOUS_USER_ID)
            {
                return (_translationProvider.Admin[lang, "CANT_DELETE_ANONYMOUS_USER"], false);
            }

            var user = await _sqlExecuter.QueryFirstOrDefaultAsync<PhpbbUsers>(
                "SELECT * FROM phpbb_users WHERE user_id = @userId",
                new { userId });
            if (user == null)
            {
                return (string.Format(_translationProvider.Admin[lang, "USER_DOESNT_EXIST_FORMAT"], userId ?? 0), false);
            }

            try
            {
                string? message = null;
                bool? isSuccess = null;
                var forumName = _config.GetValue<string>("ForumName");
                switch (action)
                {
                    case AdminUserActions.Activate:
                        {
                            await _emailService.SendEmail(
                                to: user.UserEmail,
                                subject: string.Format(_translationProvider.Email[user.UserLang, "ACCOUNT_ACTIVATED_NOTIFICATION_SUBJECT_FORMAT"], forumName),
                                bodyRazorViewName: "_AccountActivatedNotification",
                                bodyRazorViewModel: new SimpleEmailBody(user.Username, user.UserLang));

                            await _sqlExecuter.ExecuteAsync(
                                @"UPDATE phpbb_users
                                     SET user_inactive_reason = @userInactiveReason
                                        ,user_inactive_time = 0
                                        ,user_reminded = 0
                                        ,user_reminded_time = 0
                                   WHERE user_id = @userId",
                                new 
                                { 
                                    userInactiveReason = UserInactiveReason.NotInactive,
                                    userId 
                                });

                            message = string.Format(_translationProvider.Admin[lang, "USER_ACTIVATED_FORMAT"], user.Username);
                            isSuccess = true;
                            break;
                        }
                    case AdminUserActions.Activate_WithUnregisteredEmail:
                        {
                            message = user.UserInactiveReason switch
                            {
                                UserInactiveReason.ChangedEmailNotConfirmed => string.Format(_translationProvider.Admin[lang, "USER_ACTIVATED_FORMAT"], user.Username),
                                UserInactiveReason.NotInactive => string.Format(_translationProvider.BasicText[lang, "VERIFICATION_REQUEST_SUBMITTED"], user.UserEmail),
                                _ => throw new ArgumentException($"'{action}' can not be applied to a user having status '{user.UserInactiveReason}'.")
                            };

							await _sqlExecuter.ExecuteAsync(
	                            @"UPDATE phpbb_users
                                     SET user_inactive_reason = @userInactiveReason
                                        ,user_inactive_time = 0
                                        ,user_reminded = 0
                                        ,user_reminded_time = 0
                                   WHERE user_id = @userId",
	                            new
	                            {
		                            userInactiveReason = UserInactiveReason.Active_NotConfirmed,
		                            userId
	                            });
							isSuccess = true;
                            break;
                        }
                    case AdminUserActions.Deactivate:
                        {
							await _sqlExecuter.ExecuteAsync(
	                            @"UPDATE phpbb_users
                                     SET user_inactive_reason = @userInactiveReason
                                        ,user_inactive_time = @userInactiveTime
                                        ,user_should_sign_in = 1
                                   WHERE user_id = @userId",
	                            new
	                            {
		                            userInactiveReason = UserInactiveReason.InactivatedByAdmin,
                                    userInactiveTime = DateTime.UtcNow.ToUnixTimestamp(),
		                            userId
	                            });
                            message = string.Format(_translationProvider.Admin[lang, "USER_DEACTIVATED_FORMAT"], user.Username);
                            isSuccess = true;
                            break;
                        }
                    case AdminUserActions.Delete_KeepMessages:
                        {
                            using var transaction = _sqlExecuter.BeginTransaction();
                            await transaction.ExecuteAsync(
                                @"UPDATE phpbb_posts
                                     SET post_username = @username
                                        ,poster_id = @ANONYMOUS_USER_ID
                                   WHERE poster_id = @userId",
                                new
                                {
                                    Constants.ANONYMOUS_USER_ID,
                                    userId,
                                    user.Username
                                });

                            await deleteUser(transaction);
                            transaction.CommitTransaction();
                            message = string.Format(_translationProvider.Admin[lang, "USER_DELETED_POSTS_KEPT_FORMAT"], user.Username);
                            isSuccess = true;
                            break;
                        }
                    case AdminUserActions.Delete_DeleteMessages:
                        {
                            using var transaction = _sqlExecuter.BeginTransaction();
                            var toDelete = await transaction.QueryAsync<PhpbbPosts>(
                                "SELECT * FROM phpbb_posts WHERE poster_id = @userId",
                                new { userId });
                            await transaction.ExecuteAsync(
                                "DELETE FROM phpbb_posts WHERE post_id IN @postIds",
                                new
                                {
                                    postIds = toDelete.Select(p => p.PostId).DefaultIfEmpty()
                                });
                            toDelete.AsList().ForEach(async p => await _moderatorService.CascadePostDelete(p, false, false, transaction));
                            await transaction.ExecuteAsync(
                                @"UPDATE phpbb_users SET user_should_sign_in = 1 WHERE user_id = @userId",
                                new { userId });
                            await deleteUser(transaction);
                            transaction.CommitTransaction();
                            message = string.Format(_translationProvider.Admin[lang, "USER_DELETED_POSTS_DELETED_FORMAT"], user.Username);
                            isSuccess = true;
                            break;
                        }
                    case AdminUserActions.Remind:
                        {
                            string subject;
                            WelcomeEmailDto model;
                            if (user.UserInactiveReason == UserInactiveReason.NewlyRegisteredNotConfirmed)
                            {
                                subject = string.Format(_translationProvider.Email[user.UserLang, "WELCOME_REMINDER_SUBJECT_FORMAT"], forumName);
                                model = new WelcomeEmailDto(subject, user.UserActkey, user.Username, user.UserLang)
                                {
                                    IsRegistrationReminder = true,
                                    RegistrationDate = user.UserRegdate.ToUtcTime(),
                                };
                            }
                            else if (user.UserInactiveReason == UserInactiveReason.ChangedEmailNotConfirmed)
                            {
                                subject = string.Format(_translationProvider.Email[user.UserLang, "EMAIL_CHANGED_REMINDER_SUBJECT_FORMAT"], forumName);
                                model = new WelcomeEmailDto(subject, user.UserActkey, user.Username, user.UserLang)
                                {
                                    IsEmailChangeReminder = true,
                                    EmailChangeDate = user.UserInactiveTime.ToUtcTime(),
                                };
                            }
                            else
                            {
                                message = string.Format(_translationProvider.Admin[lang, "CANT_REMIND_INVALID_USER_STATE_FORMAT"], user.Username, _translationProvider.Enums[lang, user.UserInactiveReason]);
                                isSuccess = false;
                                break;
                            }

                            await _emailService.SendEmail(
                                to: user.UserEmail,
                                subject: subject,
                                bodyRazorViewName: "_WelcomeEmailPartial",
                                bodyRazorViewModel: model);

                            user.UserReminded = 1;
                            user.UserRemindedTime = DateTime.UtcNow.ToUnixTimestamp();
                            await _sqlExecuter.ExecuteAsync(
                                @"UPDATE phpbb_users
                                     SET user_reminded = 1
                                        ,user_reminded_time = @userRemindedTime
                                   WHERE user_id = @userId",
                                new
                                {
                                    userRemindedTime = DateTime.UtcNow.ToUnixTimestamp(),
                                    userId
                                });
                            message = string.Format(_translationProvider.Admin[lang, "USER_REMINDED_FORMAT"], user.Username);
                            isSuccess = true;
                            break;
                        }
                    default: throw new ArgumentException($"Unknown action '{action}'.", nameof(action));
                }

                if (isSuccess ?? false)
                {
                    await _operationLogService.LogAdminUserAction(action.Value, adminUserId, user);
                }

                return (message, isSuccess);
            }
            catch (Exception ex)
            {
                var id = _logger.ErrorWithId(ex);
                return (string.Format(_translationProvider.Errors[lang, "AN_ERROR_OCCURRED_TRY_AGAIN_ID_FORMAT"], id), false);
            }

            Task deleteUser(ITransactionalSqlExecuter transaction)
                => transaction.ExecuteAsync(
                    @"DELETE FROM phpbb_acl_users WHERE user_id = @userId;
                    DELETE FROM phpbb_banlist WHERE ban_userid = @userId;
                    DELETE FROM phpbb_bots WHERE user_id = @userId;
                    DELETE FROM phpbb_drafts WHERE user_id = @userId;
                    UPDATE phpbb_forums
                         SET forum_last_poster_id = @ANONYMOUS_USER_ID
                            ,forum_last_poster_colour = ''
                            ,forum_last_poster_name = @username
                       WHERE forum_last_poster_id = @userId;
                    DELETE FROM phpbb_forums_track WHERE user_id = @userId;
                    DELETE FROM phpbb_forums_watch WHERE user_id = @userId;
                    DELETE FROM phpbb_log WHERE user_id = @userId;
                    DELETE FROM phpbb_poll_votes WHERE vote_user_id = @userId;
                    DELETE FROM phpbb_privmsgs_to WHERE user_id = @userId;
                    DELETE FROM phpbb_reports WHERE user_id = @userId;
                    UPDATE phpbb_topics
                         SET topic_last_poster_id = @ANONYMOUS_USER_ID
                            ,topic_last_poster_colour = ''
                            ,topic_last_poster_name = @username
                       WHERE topic_last_poster_id = @userId;
                    UPDATE phpbb_topics
                         SET topic_first_poster_colour = ''
                       WHERE topic_first_poster_name = @username;
                    DELETE FROM phpbb_topics_track WHERE user_id = @userId;
                    DELETE FROM phpbb_topics_watch WHERE user_id = @userId;
                    DELETE FROM phpbb_user_group WHERE user_id = @userId;
                    DELETE FROM phpbb_user_topic_post_number WHERE user_id = @userId;
                    DELETE FROM phpbb_zebra WHERE user_id = @userId;
                    DELETE FROM phpbb_users WHERE user_id = @userId;", 
                    new
                    {
                        Constants.ANONYMOUS_USER_ID,
                        user.Username,
                        userId
                    });
        }

        public async Task<(string? Message, bool IsSuccess, List<PhpbbUsers> Result)> UserSearchAsync(AdminUserSearch? searchParameters)
        {
            var lang = _translationProvider.GetLanguage();
            try
            {              
                var sql = new StringBuilder(
					@"SELECT DISTINCT u.* 
                        FROM phpbb_users u
                        JOIN phpbb_user_group ug ON u.user_id = ug.user_id
                       WHERE u.user_id <> @ANONYMOUS_USER_ID 
                         AND user_regdate >= @rf 
                         AND user_regdate <= @rt
                         AND ug.group_id <> @BOTS_GROUP_ID
                         AND ug.group_id <> @GUESTS_GROUP_ID");

                var param = new DynamicParameters(new
                { 
                    Constants.ANONYMOUS_USER_ID, 
                    rf = ParseDate(searchParameters?.RegisteredFrom, false), 
                    rt = ParseDate(searchParameters?.RegisteredTo, true),
                    Constants.BOTS_GROUP_ID,
                    Constants.GUESTS_GROUP_ID
                });

                if (!string.IsNullOrWhiteSpace(searchParameters?.Username))
                {
                    sql.AppendLine(" AND u.username_clean LIKE @username");
                    param.Add("username", $"%{StringUtility.CleanString(searchParameters.Username)}%");
				}
				if (!string.IsNullOrWhiteSpace(searchParameters?.Email))
                {
					sql.AppendLine(" AND u.user_email_hash = @emailHash");
					param.Add("emailHash", HashUtility.ComputeCrc64Hash(searchParameters.Email.Trim()));
				}
                if (searchParameters?.UserId > 0)
                {
					sql.AppendLine(" AND u.user_id = @userId");
					param.Add("userId", searchParameters.UserId);
				}

                if (searchParameters?.NeverActive != true)
                {
					sql.AppendLine(" AND user_lastvisit >= @laf AND user_lastvisit <= @lat");
					param.Add("laf", ParseDate(searchParameters?.LastActiveFrom, false));
					param.Add("lat", ParseDate(searchParameters?.LastActiveTo, true));
                }
                else
                {
					sql.AppendLine(" AND u.user_lastvisit = 0");
                }

                sql.AppendLine(" ORDER BY u.username_clean ASC");

                var result = await _sqlExecuter.QueryAsync<PhpbbUsers>(sql.ToString(), param);
                return (null, true, result.AsList());
            }
            catch (DateInputException die)
            {
                _logger.ErrorWithId(die.InnerException!, die.Message);
                return (_translationProvider.Admin[lang, "ONE_OR_MORE_INVALID_INPUT_DATES"], false, new List<PhpbbUsers>());
            }
            catch (Exception ex)
            {
                var id = _logger.ErrorWithId(ex);
                return (string.Format(_translationProvider.Errors[lang, "AN_ERROR_OCCURRED_TRY_AGAIN_ID_FORMAT"], id), false, new List<PhpbbUsers>());
            }

            long ParseDate(string? value, bool isUpperLimit)
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    return isUpperLimit ? DateTime.UtcNow.ToUnixTimestamp() : 0L;
                }

                try
                {
                    var toReturn = DateTime.ParseExact(value, "dd.MM.yyyy", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal);
                    return (isUpperLimit ? toReturn.AddDays(1).AddMilliseconds(-1) : toReturn).ToUnixTimestamp();
                }
                catch (Exception ex)
                {
                    throw new DateInputException(ex);
                }
            }
        }

        class DateInputException : Exception
        {
            internal DateInputException(Exception inner) : base("Failed to parse exact input dates", inner)
            {

            }
        }

        #endregion User

        #region Rank

        public async Task<(string Message, bool? IsSuccess)> ManageRank(int? rankId, string rankName, bool? deleteRank, int adminUserId)
        {
            var lang = _translationProvider.GetLanguage();
            try
            {
                if (string.IsNullOrWhiteSpace(rankName))
                {
                    return (_translationProvider.Admin[lang, "INVALID_RANK_NAME"], false);
                }

                AdminRankActions action;
                var actual = await _sqlExecuter.QueryFirstOrDefaultAsync<PhpbbRanks>(
                    "SELECT * FROM phpbb_ranks WHERE rank_id = @rankId",
                    new
                    {
                        rankId = rankId ?? 0
                    });
                if ((rankId ?? 0) == 0)
                {
                    actual = await _sqlExecuter.QueryFirstOrDefaultAsync<PhpbbRanks>(
                        @$"INSERT INTO phpbb_ranks (rank_title) VALUES (@rankName);
                           SELECT * FROM phpbb_ranks WHERE rank_id = {_sqlExecuter.LastInsertedItemId}",
                        new { rankName });
                    action = AdminRankActions.Add;
                }
                else if (deleteRank ?? false)
                {
                    var rows = await _sqlExecuter.ExecuteAsync(
                        "DELETE FROM phpbb_ranks WHERE rank_id = @rankId",
                        new { rankId });
                    if (rows == 0)
                    {
                        return (string.Format(_translationProvider.Admin[lang, "RANK_DOESNT_EXIST_FORMAT"], rankId), false);
                    }
                    action = AdminRankActions.Delete;
                }
                else
                {
                    var rows = await _sqlExecuter.ExecuteAsync(
                        "UPDATE phpbb_ranks SET rank_title = @rankName WHERE rank_id = @rankId",
                        new
                        {
                            rankId,
                            rankName
                        });
                    if (rows == 0)
                    {
                        return (string.Format(_translationProvider.Admin[lang, "RANK_DOESNT_EXIST_FORMAT"], rankId), false);
                    }
                    action = AdminRankActions.Update;
                }

                await _operationLogService.LogAdminRankAction(action, adminUserId, actual);

                return (_translationProvider.Admin[lang, "RANK_UPDATED_SUCCESSFULLY"], true);
            }
            catch (Exception ex)
            {
                var id = _logger.ErrorWithId(ex);
                return (string.Format(_translationProvider.Errors[lang, "AN_ERROR_OCCURRED_TRY_AGAIN_ID_FORMAT"], id), false);
            }
        }

        #endregion Rank

        #region Group

        public async Task<List<UpsertGroupDto>> GetGroups()
            => (await _sqlExecuter.CallStoredProcedureAsync<UpsertGroupDto>("get_all_groups")).AsList();

        public async Task<(string Message, bool? IsSuccess)> ManageGroup(UpsertGroupDto dto, int adminUserId)
        {
            var lang = _translationProvider.GetLanguage();
            try
            {
                AdminGroupActions action;
                var changedColor = false;
				var actual = await _sqlExecuter.QueryFirstOrDefaultAsync<PhpbbGroups>(
	                "SELECT * FROM phpbb_groups WHERE group_id = @id",
	                new { dto.Id });
				if (dto.Id == 0)
                {
                    actual = await _sqlExecuter.QueryFirstOrDefaultAsync<PhpbbGroups>(
                        @$"INSERT INTO phpbb_groups (group_name, group_desc, group_rank, group_colour, group_user_upload_size, group_edit_time) 
                                  VALUES(@name, @desc, @rank, @dbColor, @uploadLimit, @editTime);
                           SELECT * FROM phpbb_groups where group_id = {_sqlExecuter.LastInsertedItemId}",
                        new
                        {
                            name = dto.Name ?? string.Empty,
                            desc = dto.Desc ?? string.Empty,
                            dto.Rank,
                            dbColor = dto.DbColor ?? string.Empty,
                            uploadLimit = dto.UploadLimit * 1024 * 1024,
                            dto.EditTime
                        });
                    action = AdminGroupActions.Add;
                }
                else
                {
                    if (actual is null)
                    {
                        return (string.Format(_translationProvider.Admin[lang, "GROUP_DOESNT_EXIST"], dto.Id), false);
                    }

                    if (dto.Delete == true)
                    {
                        var userCount = await _sqlExecuter.ExecuteScalarAsync<long>(
                            "SELECT count(1) FROM phpbb_users WHERE group_id = @id",
                            new { dto.Id });
                        if (userCount > 0)
                        {
                            return (string.Format(_translationProvider.Admin[lang, "CANT_DELETE_NOT_EMPTY_FORMAT"], actual.GroupName), false);
                        }
                        await _sqlExecuter.ExecuteAsync(
                            @"DELETE FROM phpbb_groups WHERE group_id = @id;
                              DELETE FROM phpbb_user_group WHERE group_id = @id;",
                              new { dto.Id });
                        actual = null;
                        action = AdminGroupActions.Delete;
                    }
                    else
                    {
                        changedColor = !actual.GroupColour.Equals(dto.DbColor, StringComparison.InvariantCultureIgnoreCase);
                        await _sqlExecuter.ExecuteAsync(
							@"UPDATE phpbb_groups 
                                 SET group_name = @name
                                    ,group_desc = @desc
                                    ,group_rank = @rank
                                    ,group_colour = @dbColor
                                    ,group_user_upload_size = @uploadLimit
                                    ,group_edit_time = @editTime
                               WHERE group_id = @id;",
						    new
						    {
                                name = dto.Name ?? string.Empty,
                                desc = dto.Desc ?? string.Empty,
                                dto.Rank,
                                dbColor = dto.DbColor ?? string.Empty,
                                uploadLimit = dto.UploadLimit * 1024 * 1024,
                                dto.EditTime,
                                dto.Id
                            });
						action = AdminGroupActions.Update;
                    }
                }

                if (actual is not null)
                {
                    var currentRole = await _sqlExecuter.QueryFirstOrDefaultAsync<PhpbbAclGroups>(
                        "SELECT * FROM phpbb_acl_groups WHERE group_id = @groupId AND forum_id = 0",
                        new { actual.GroupId });
                    if (currentRole != null)
                    {
                        if (dto.Role == 0)
                        {
                            await _sqlExecuter.ExecuteAsync(
                                "DELETE FROM phpbb_acl_groups WHERE group_id = @groupId AND forum_id = 0",
                                new { actual.GroupId });
                        }
                        else if (currentRole.AuthRoleId != dto.Role && await roleIsValid(dto.Role))
                        {
							await _sqlExecuter.ExecuteAsync(
								"DELETE FROM phpbb_acl_groups WHERE group_id = @groupId AND forum_id = 0",
								new { actual.GroupId });
							currentRole = null;
                        }
                    }
                    if (currentRole == null && dto.Role != 0 && await roleIsValid(dto.Role))
                    {
                        await _sqlExecuter.ExecuteAsync(
                            "INSERT INTO phpbb_acl_groups (group_id, auth_role_id, forum_id) VALUES (@groupId, @role, 0)",
                            new { actual.GroupId, dto.Role });
                    }
                }

                if (changedColor)
                {
                    var userIds = await _sqlExecuter.QueryAsync<int>(
                        "SELECT user_id FROM phpbb_users WHERE group_id = @id",
                        new { dto.Id });

                    await _sqlExecuter.ExecuteAsync(
                        @"UPDATE phpbb_users SET user_colour = @dbColor WHERE user_id IN @userIds;
                          UPDATE phpbb_topics SET topic_last_poster_colour = @dbColor WHERE topic_last_poster_id IN @userIds;
                          UPDATE phpbb_forums SET forum_last_poster_colour = @dbColor WHERE forum_last_poster_id IN @userIds;",
                        new
                        {
                            userIds = userIds.DefaultIfEmpty(),
                            dbColor = dto.DbColor ?? string.Empty
                        });
                }

                if (actual is not null)
                {
                    await _operationLogService.LogAdminGroupAction(action, adminUserId, actual);
                }

                var message = action switch
                {
                    AdminGroupActions.Add => _translationProvider.Admin[lang, "GROUP_ADDED_SUCCESSFULLY"],
                    AdminGroupActions.Delete => _translationProvider.Admin[lang, "GROUP_DELETED_SUCCESSFULLY"],
                    AdminGroupActions.Update => _translationProvider.Admin[lang, "GROUP_UPDATED_SUCCESSFULLY"],
                    _ => _translationProvider.Admin[lang, "GROUP_UPDATED_SUCCESSFULLY"],
                };
                return (message, true);
            }
            catch (Exception ex)
            {
                var id = _logger.ErrorWithId(ex);
                return (string.Format(_translationProvider.Errors[lang, "AN_ERROR_OCCURRED_TRY_AGAIN_ID_FORMAT"], id), false);
            }

            async Task<bool> roleIsValid(int roleId)
                => await _sqlExecuter.ExecuteScalarAsync<long>("SELECT count(1) FROM phpbb_acl_roles WHERE role_id = @roleId", new { roleId }) > 0;
		}

		public async Task<List<SelectListItem>> GetRanksSelectListItems()
        {
            var lang = _translationProvider.GetLanguage();
            var groupRanks = new List<SelectListItem> { new SelectListItem(_translationProvider.Admin[lang, "NO_RANK"], "0", true) };
            var allRanks = await _sqlExecuter.QueryAsync<PhpbbRanks>("SELECT * FROM phpbb_ranks");
            groupRanks.AddRange(allRanks.Select(x => new SelectListItem(x.RankTitle, x.RankId.ToString())));
            return groupRanks;
        }

        public async Task<List<SelectListItem>> GetRolesSelectListItems()
        {
            var lang = _translationProvider.GetLanguage();
            var roles = new List<SelectListItem> { new SelectListItem(_translationProvider.Admin[lang, "NO_ROLE"], "0", true) };
            var allRoles = await _sqlExecuter.QueryAsync<PhpbbAclRoles>("SELECT * FROM phpbb_acl_roles WHERE role_type = 'u_'");
			roles.AddRange(allRoles.Select(x => new SelectListItem(_translationProvider.Admin[lang, x.RoleName, Casing.None, x.RoleName], x.RoleId.ToString())));
            return roles;
        }

        #endregion Group

        #region Banlist

        public async Task<(string Message, bool? IsSuccess)> BanUser(List<UpsertBanListDto> banlist, List<int> indexesToRemove, int adminUserId)
        {
            var lang = _translationProvider.GetLanguage();
            try
            {
                var indexHash = new HashSet<int>(indexesToRemove);
                var exceptions = new List<Exception>();
                for (var i = 0; i < banlist.Count; i++)
                {
                    try
                    {
                        AdminBanListActions? action = null;
                        if (indexHash.Contains(i))
                        {
                            await _sqlExecuter.ExecuteAsync("DELETE FROM phpbb_banlist WHERE ban_id = @BanId", banlist[i]);
                            action = AdminBanListActions.Delete;
                        }
                        else if (banlist[i].BanId == 0)
                        {
                            await _sqlExecuter.ExecuteAsync("INSERT INTO phpbb_banlist (ban_ip, ban_email) VALUES (@BanIp, @BanEmail)", banlist[i]);
                            action = AdminBanListActions.Add;
                        }
                        else if (banlist[i].BanEmail != banlist[i].BanEmailOldValue || banlist[i].BanIp != banlist[i].BanIpOldValue)
                        {
                            await _sqlExecuter.ExecuteAsync("UPDATE phpbb_banlist SET ban_email = @BanEmail, ban_ip = @BanIp WHERE ban_id = @BanId", banlist[i]);
                            action = AdminBanListActions.Update;
                        }
                        if (action != null)
                        {
                            await _operationLogService.LogAdminBanListAction(action.Value, adminUserId, banlist[i]);
                        }
                    }
                    catch (Exception ex)
                    {
                        exceptions.Add(ex);
                    }
                }
                if (exceptions.Any())
                {
                    throw new AggregateException(exceptions);
                }
                return (_translationProvider.Admin[lang, "BANLIST_UPDATED_SUCCESSFULLY"], true);
            }
            catch (Exception ex)
            {
                var id = _logger.ErrorWithId(ex);
                return (string.Format(_translationProvider.Errors[lang, "AN_ERROR_OCCURRED_TRY_AGAIN_ID_FORMAT"], id), false);
            }
        }

        public async Task<List<UpsertBanListDto>> GetBanList()
            => (await _sqlExecuter.QueryAsync<UpsertBanListDto>(
                "SELECT ban_id, ban_email, ban_email AS ban_email_old_value, ban_ip, ban_ip AS ban_ip_old_value FROM phpbb_banlist")).AsList();

        #endregion Banlist
    }
}
