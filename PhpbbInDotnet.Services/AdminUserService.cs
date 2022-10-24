using Dapper;
using LazyCache;
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
using Serilog;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace PhpbbInDotnet.Services
{
    class AdminUserService : IAdminUserService
    {
        private readonly IForumDbContext _context;
        private readonly IPostService _postService;
        private readonly IAppCache _cache;
        private readonly IConfiguration _config;
        private readonly IOperationLogService _operationLogService;
        private readonly ITranslationProvider _translationProvider;
        private readonly ILogger _logger;
        private readonly IEmailService _emailService;

        public AdminUserService(IForumDbContext context, IPostService postService, IAppCache cache, IConfiguration config, ITranslationProvider translationProvider,
            IOperationLogService operationLogService, ILogger logger, IEmailService emailService)
        {
            _context = context;
            _postService = postService;
            _cache = cache;
            _config = config;
            _operationLogService = operationLogService;
            _translationProvider = translationProvider;
            _logger = logger;
            _emailService = emailService;
        }

        #region User

        public async Task<List<PhpbbUsers>> GetInactiveUsers()
            => await (
                from u in _context.PhpbbUsers.AsNoTracking()
                where u.UserInactiveTime > 0
                    && u.UserInactiveReason != UserInactiveReason.NotInactive
                orderby u.UserInactiveTime descending
                select u
            ).ToListAsync();

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
                var users = await (
                    from u in _context.PhpbbUsers
                    where userIds.Contains(u.UserId) && u.UserInactiveReason == UserInactiveReason.NewlyRegisteredNotConfirmed
                    select u
                ).ToListAsync();

                _context.PhpbbUsers.RemoveRange(users);
                await _context.SaveChangesAsync();

                if (users.Count == userIds.Length)
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

            var user = await _context.PhpbbUsers.FirstOrDefaultAsync(u => u.UserId == userId);
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

                            user.UserInactiveReason = UserInactiveReason.NotInactive;
                            user.UserInactiveTime = 0L;
                            user.UserReminded = 0;
                            user.UserRemindedTime = 0L;
                            message = string.Format(_translationProvider.Admin[lang, "USER_ACTIVATED_FORMAT"], user.Username);
                            isSuccess = true;
                            break;
                        }
                    case AdminUserActions.Deactivate:
                        {
                            user.UserInactiveReason = UserInactiveReason.InactivatedByAdmin;
                            user.UserInactiveTime = DateTime.UtcNow.ToUnixTimestamp();
                            user.UserShouldSignIn = true;
                            message = string.Format(_translationProvider.Admin[lang, "USER_DEACTIVATED_FORMAT"], user.Username);
                            isSuccess = true;
                            break;
                        }
                    case AdminUserActions.Delete_KeepMessages:
                        {
                            var posts = await (
                                from p in _context.PhpbbPosts
                                where p.PosterId == userId
                                select p
                            ).ToListAsync();

                            posts.ForEach(p =>
                            {
                                p.PostUsername = user.Username;
                                p.PosterId = Constants.ANONYMOUS_USER_ID;
                            });

                            user.UserShouldSignIn = true;
                            await deleteUser();
                            message = string.Format(_translationProvider.Admin[lang, "USER_DELETED_POSTS_KEPT_FORMAT"], user.Username);
                            isSuccess = true;
                            break;
                        }
                    case AdminUserActions.Delete_DeleteMessages:
                        {
                            var toDelete = await _context.PhpbbPosts.Where(p => p.PosterId == userId).ToListAsync();
                            _context.PhpbbPosts.RemoveRange(toDelete);
                            await _context.SaveChangesAsync();
                            toDelete.ForEach(async p => await _postService.CascadePostDelete(p, false, false));
                            user.UserShouldSignIn = true;
                            await deleteUser();
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
                            message = string.Format(_translationProvider.Admin[lang, "USER_REMINDED_FORMAT"], user.Username);
                            isSuccess = true;
                            break;
                        }
                    default: throw new ArgumentException($"Unknown action '{action}'.", nameof(action));
                }

                await _context.SaveChangesAsync();

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

            async Task deleteUser()
            {
                _context.PhpbbAclUsers.RemoveRange(_context.PhpbbAclUsers.Where(u => u.UserId == userId));
                _context.PhpbbBanlist.RemoveRange(_context.PhpbbBanlist.Where(u => u.BanUserid == userId));
                _context.PhpbbBots.RemoveRange(_context.PhpbbBots.Where(u => u.UserId == userId));
                _context.PhpbbDrafts.RemoveRange(_context.PhpbbDrafts.Where(u => u.UserId == userId));
                (await _context.PhpbbForums.Where(f => f.ForumLastPosterId == userId).ToListAsync()).ForEach(f =>
                {
                    f.ForumLastPosterId = 1;
                    f.ForumLastPosterColour = string.Empty;
                    f.ForumLastPosterName = user.Username;
                });
                _context.PhpbbForumsTrack.RemoveRange(_context.PhpbbForumsTrack.Where(u => u.UserId == userId));
                _context.PhpbbForumsWatch.RemoveRange(_context.PhpbbForumsWatch.Where(u => u.UserId == userId));
                _context.PhpbbLog.RemoveRange(_context.PhpbbLog.Where(u => u.UserId == userId));
                await _context.GetSqlExecuter().ExecuteAsync("DELETE FROM phpbb_poll_votes WHERE vote_user_id = @userId", new { userId });
                _context.PhpbbPrivmsgsTo.RemoveRange(_context.PhpbbPrivmsgsTo.Where(u => u.UserId == userId));
                _context.PhpbbReports.RemoveRange(_context.PhpbbReports.Where(u => u.UserId == userId));
                (await _context.PhpbbTopics.Where(t => t.TopicLastPosterId == userId).ToListAsync()).ForEach(t =>
                {
                    t.TopicLastPosterId = 1;
                    t.TopicLastPosterColour = string.Empty;
                    t.TopicLastPosterName = user.Username;
                });
                (await _context.PhpbbTopics.Where(t => t.TopicFirstPosterName == user.Username).ToListAsync()).ForEach(t =>
                {
                    t.TopicFirstPostId = 1;
                    t.TopicFirstPosterColour = string.Empty;
                });
                _context.PhpbbTopicsTrack.RemoveRange(_context.PhpbbTopicsTrack.Where(u => u.UserId == userId));
                _context.PhpbbTopicsWatch.RemoveRange(_context.PhpbbTopicsWatch.Where(u => u.UserId == userId));
                _context.PhpbbUserGroup.RemoveRange(_context.PhpbbUserGroup.Where(u => u.UserId == userId));
                _context.PhpbbUsers.RemoveRange(_context.PhpbbUsers.Where(u => u.UserId == userId));
                _context.PhpbbUserTopicPostNumber.RemoveRange(_context.PhpbbUserTopicPostNumber.Where(u => u.UserId == userId));
                _context.PhpbbZebra.RemoveRange(_context.PhpbbZebra.Where(u => u.UserId == userId));
                _context.PhpbbUsers.Remove(user);
            }
        }

        public async Task<(string? Message, bool IsSuccess, List<PhpbbUsers> Result)> UserSearchAsync(AdminUserSearch? searchParameters)
        {
            var lang = _translationProvider.GetLanguage();
            try
            {
                var rf = ParseDate(searchParameters?.RegisteredFrom, false);
                var rt = ParseDate(searchParameters?.RegisteredTo, true);
                var username = searchParameters?.Username;
                var email = searchParameters?.Email?.Trim();
                var userId = searchParameters?.UserId ?? 0;
                var query = from u in _context.PhpbbUsers.AsNoTracking()
                            where (string.IsNullOrWhiteSpace(username) || u.UsernameClean.Contains(StringUtility.CleanString(username)))
                                && (string.IsNullOrWhiteSpace(email) || u.UserEmailHash == HashUtility.ComputeCrc64Hash(email))
                                && (userId == 0 || u.UserId == userId)
                                && u.UserRegdate >= rf && u.UserRegdate <= rt
                                && u.UserId != Constants.ANONYMOUS_USER_ID
                            join ug in _context.PhpbbUserGroup.AsNoTracking()
                            on u.UserId equals ug.UserId into joined
                            from j in joined.DefaultIfEmpty()
                            let groupId = j == null ? u.GroupId : j.GroupId
                            where groupId != Constants.BOTS_GROUP_ID && groupId != Constants.GUESTS_GROUP_ID
                            select u;

                if (!(searchParameters?.NeverActive ?? false))
                {
                    var laf = ParseDate(searchParameters?.LastActiveFrom, false);
                    var lat = ParseDate(searchParameters?.LastActiveTo, true);
                    query = from q in query
                            where q.UserLastvisit >= laf && q.UserLastvisit <= lat
                            select q;
                }
                else
                {
                    query = from q in query
                            where q.UserLastvisit == 0L
                            select q;
                }

                query = (
                    from q in query
                    orderby q.UsernameClean ascending
                    select q
                ).Distinct();

                return (null, true, await query.ToListAsync());
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
                PhpbbRanks? actual;
                if ((rankId ?? 0) == 0)
                {
                    actual = new PhpbbRanks
                    {
                        RankTitle = rankName
                    };
                    var result = await _context.PhpbbRanks.AddAsync(actual);
                    result.Entity.RankId = 0;
                    action = AdminRankActions.Add;
                }
                else if (deleteRank ?? false)
                {
                    actual = await _context.PhpbbRanks.FirstOrDefaultAsync(x => x.RankId == rankId);
                    if (actual is null)
                    {
                        return (string.Format(_translationProvider.Admin[lang, "RANK_DOESNT_EXIST_FORMAT"], rankId), false);
                    }
                    _context.PhpbbRanks.Remove(actual);
                    action = AdminRankActions.Delete;
                }
                else
                {
                    actual = await _context.PhpbbRanks.FirstOrDefaultAsync(x => x.RankId == rankId);
                    if (actual == null)
                    {
                        return (string.Format(_translationProvider.Admin[lang, "RANK_DOESNT_EXIST_FORMAT"], rankId), false);
                    }
                    actual.RankTitle = rankName;
                    action = AdminRankActions.Update;
                }
                await _context.SaveChangesAsync();

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
            => (
                await (_context.GetSqlExecuter()).QueryAsync<UpsertGroupDto>(
                    @"SELECT g.group_id AS id, 
                             g.group_name AS `name`,
                             g.group_desc AS `desc`,
                             g.group_rank AS `rank`,
                             concat('#', g.group_colour) AS color,
                             g.group_edit_time AS edit_time,
                             g.group_user_upload_size AS upload_limit,
                             coalesce(
		                         (SELECT r.auth_role_id 
		                            FROM phpbb_acl_groups r  
		                           WHERE g.group_id = r.group_id AND r.forum_id = 0
		                           LIMIT 1)
                                 , 0
                             ) as role
                        FROM phpbb_groups g"
                )
            ).AsList();

        public async Task<(string Message, bool? IsSuccess)> ManageGroup(UpsertGroupDto dto, int adminUserId)
        {
            var lang = _translationProvider.GetLanguage();
            try
            {
                void update(PhpbbGroups destination, UpsertGroupDto source)
                {
                    destination.GroupName = source.Name!;
                    destination.GroupDesc = source.Desc!;
                    destination.GroupRank = source.Rank;
                    destination.GroupColour = source.DbColor!;
                    destination.GroupUserUploadSize = source.UploadLimit * 1024 * 1024;
                    destination.GroupEditTime = source.EditTime;
                }

                async Task<bool> roleIsValid(int roleId) => await _context.PhpbbAclRoles.FirstOrDefaultAsync(x => x.RoleId == roleId) != null;

                AdminGroupActions action;
                var changedColor = false;
                PhpbbGroups? actual;
                if (dto.Id == 0)
                {
                    actual = new PhpbbGroups();
                    update(actual, dto);
                    var result = await _context.PhpbbGroups.AddAsync(actual);
                    result.Entity.GroupId = 0;
                    await _context.SaveChangesAsync();
                    actual = result.Entity;
                    action = AdminGroupActions.Add;
                }
                else
                {
                    actual = await _context.PhpbbGroups.FirstOrDefaultAsync(x => x.GroupId == dto.Id);
                    if (actual == null)
                    {
                        return (string.Format(_translationProvider.Admin[lang, "GROUP_DOESNT_EXIST"], dto.Id), false);
                    }

                    if (dto.Delete ?? false)
                    {
                        if (await _context.PhpbbUsers.AsNoTracking().CountAsync(x => x.GroupId == dto.Id) > 0)
                        {
                            return (string.Format(_translationProvider.Admin[lang, "CANT_DELETE_NOT_EMPTY_FORMAT"], actual.GroupName), false);
                        }
                        _context.PhpbbGroups.Remove(actual);
                        actual = null;
                        action = AdminGroupActions.Delete;
                    }
                    else
                    {
                        changedColor = !actual.GroupColour.Equals(dto.DbColor, StringComparison.InvariantCultureIgnoreCase);
                        update(actual, dto);
                        action = AdminGroupActions.Update;
                    }
                    await _context.SaveChangesAsync();
                }

                if (actual is not null)
                {
                    var currentRole = await _context.PhpbbAclGroups.FirstOrDefaultAsync(x => x.GroupId == actual.GroupId && x.ForumId == 0);
                    if (currentRole != null)
                    {
                        if (dto.Role == 0)
                        {
                            _context.PhpbbAclGroups.Remove(currentRole);
                        }
                        else if (currentRole.AuthRoleId != dto.Role && await roleIsValid(dto.Role))
                        {
                            _context.PhpbbAclGroups.Remove(currentRole);
                            await _context.SaveChangesAsync();
                            currentRole = null;
                        }
                    }
                    if (currentRole == null && dto.Role != 0 && await roleIsValid(dto.Role))
                    {
                        var result = _context.PhpbbAclGroups.Add(new PhpbbAclGroups
                        {
                            GroupId = actual.GroupId,
                            AuthRoleId = dto.Role,
                            ForumId = 0
                        });
                        result.Entity.AuthOptionId = result.Entity.AuthSetting = 0;
                    }

                    await _context.SaveChangesAsync();
                }

                if (changedColor)
                {
                    var affectedUsers = await _context.PhpbbUsers.Where(x => x.GroupId == dto.Id).ToListAsync();
                    affectedUsers.ForEach(x => x.UserColour = dto.DbColor!);
                    _context.PhpbbUsers.UpdateRange(affectedUsers);
                    var affectedTopics = await (
                        from t in _context.PhpbbTopics
                        where affectedUsers.Select(u => u.UserId).Contains(t.TopicLastPosterId)
                        select t
                    ).ToListAsync();
                    affectedTopics.ForEach(t => t.TopicLastPosterColour = dto.DbColor!);
                    _context.PhpbbTopics.UpdateRange(affectedTopics);
                    var affectedForums = await (
                        from t in _context.PhpbbForums
                        where affectedUsers.Select(u => u.UserId).Contains(t.ForumLastPosterId)
                        select t
                    ).ToListAsync();
                    affectedForums.ForEach(t => t.ForumLastPosterColour = dto.DbColor!);
                    _context.PhpbbForums.UpdateRange(affectedForums);

                    await _context.SaveChangesAsync();
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
        }

        public List<SelectListItem> GetRanksSelectListItems()
        {
            var lang = _translationProvider.GetLanguage();
            var groupRanks = new List<SelectListItem> { new SelectListItem(_translationProvider.Admin[lang, "NO_RANK"], "0", true) };
            groupRanks.AddRange(_context.PhpbbRanks.AsNoTracking().Select(x => new SelectListItem(x.RankTitle, x.RankId.ToString())));
            return groupRanks;
        }

        public List<SelectListItem> GetRolesSelectListItems()
        {
            var lang = _translationProvider.GetLanguage();
            var roles = new List<SelectListItem> { new SelectListItem(_translationProvider.Admin[lang, "NO_ROLE"], "0", true) };
            roles.AddRange(_context.PhpbbAclRoles.AsNoTracking().Where(x => x.RoleType == "u_").Select(x => new SelectListItem(_translationProvider.Admin[lang, x.RoleName, Casing.None, x.RoleName], x.RoleId.ToString())));
            return roles;
        }

        #endregion Group

        #region Banlist

        public async Task<(string Message, bool? IsSuccess)> BanUser(List<UpsertBanListDto> banlist, List<int> indexesToRemove, int adminUserId)
        {
            var lang = _translationProvider.GetLanguage();
            try
            {
                var sqlExecuter = _context.GetSqlExecuter();
                var indexHash = new HashSet<int>(indexesToRemove);
                var exceptions = new List<Exception>();
                for (var i = 0; i < banlist.Count; i++)
                {
                    try
                    {
                        AdminBanListActions? action = null;
                        if (indexHash.Contains(i))
                        {
                            await sqlExecuter.ExecuteAsync("DELETE FROM phpbb_banlist WHERE ban_id = @BanId", banlist[i]);
                            action = AdminBanListActions.Delete;
                        }
                        else if (banlist[i].BanId == 0)
                        {
                            await sqlExecuter.ExecuteAsync("INSERT INTO phpbb_banlist (ban_ip, ban_email) VALUES (@BanIp, @BanEmail)", banlist[i]);
                            action = AdminBanListActions.Add;
                        }
                        else if (banlist[i].BanEmail != banlist[i].BanEmailOldValue || banlist[i].BanIp != banlist[i].BanIpOldValue)
                        {
                            await sqlExecuter.ExecuteAsync("UPDATE phpbb_banlist SET ban_email = @BanEmail, ban_ip = @BanIp WHERE ban_id = @BanId", banlist[i]);
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
            => await (
                from b in _context.PhpbbBanlist.AsNoTracking()
                select new UpsertBanListDto
                {
                    BanId = b.BanId,
                    BanEmail = b.BanEmail,
                    BanEmailOldValue = b.BanEmail,
                    BanIp = b.BanIp,
                    BanIpOldValue = b.BanIp
                }
            ).ToListAsync();

        #endregion Banlist
    }
}
