using Dapper;
using LazyCache;
using Microsoft.Extensions.Configuration;
using PhpbbInDotnet.Database.Entities;
using PhpbbInDotnet.Database.SqlExecuter;
using PhpbbInDotnet.Domain;
using PhpbbInDotnet.Domain.Extensions;
using PhpbbInDotnet.Languages;
using PhpbbInDotnet.Objects;
using PhpbbInDotnet.Objects.EmailDtos;
using Serilog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PhpbbInDotnet.Services
{
    class UserService : IUserService
    {
        const string ANONYMOUS_DB_USER_CACHE_KEY = "AnonymousDbUserCacheKey";
        const string ANONYMOUS_FORUM_USER_CACHE_KEY = "AnonymousForumUserCacheKey";
        const string ADMIN_ROLES_CACHE_KEY = "AdminRolesCacheKey";
        const string MOD_ROLES_CACHE_KEY = "ModRolesCacheKey";
        const string USER_ROLES_CACHE_KEY = "UserRolesCacheKey";
        static readonly TimeSpan CACHE_EXPIRATION = TimeSpan.FromHours(1);

        private readonly ISqlExecuter _sqlExecuter;
        private readonly IConfiguration _config;
        private readonly ILogger _logger;
        private readonly IEmailService _emailService;
        private readonly IAppCache _cache;
        private readonly ITranslationProvider _translationProvider;

        private List<KeyValuePair<string, int>>? _userMap;
        private ConcurrentDictionary<int, HashSet<ForumUserExpanded.Permissions>> _permissionsMap = new();

        public UserService(ISqlExecuter sqlExecuter, IConfiguration config, ITranslationProvider translationProvider, ILogger logger, IEmailService emailService, IAppCache cache)
        {
            _translationProvider = translationProvider;
            _sqlExecuter = sqlExecuter;
            _config = config;
            _logger = logger;
            _emailService = emailService;
            _cache = cache;
        }

        public async Task<bool> IsAdmin(ForumUserExpanded user)
            => (from up in user.AllPermissions
                where up.ForumId == 0
                join a in await GetAdminRolesLazy()
                on up.AuthRoleId equals a.RoleId
                select up).Any();

        public async Task<bool> IsUserModeratorInForum(ForumUserExpanded user, int forumId)
            => await IsAdmin(user) ||
                (from up in user.AllPermissions
                 where up.ForumId == forumId || up.ForumId == 0
                 join a in await GetModRolesLazy()
                 on up.AuthRoleId equals a.RoleId
                 select up).Any();

        public async Task<int?> GetUserRole(ForumUserExpanded user)
            => (from up in user.AllPermissions
                join a in await GetUserRolesLazy()
                on up.AuthRoleId equals a.RoleId
                select up.AuthRoleId as int?).FirstOrDefault();

        public Task<PhpbbUsers> GetAnonymousDbUserAsync()
            => _cache.GetOrAddAsync(
                key: ANONYMOUS_DB_USER_CACHE_KEY,
                addItemFactory: () => _sqlExecuter.QuerySingleAsync<PhpbbUsers>(
                    "SELECT * FROM phpbb_users WHERE user_id = @ANONYMOUS_USER_ID",
                    new { Constants.ANONYMOUS_USER_ID }),
                expires: DateTimeOffset.UtcNow + CACHE_EXPIRATION);

        PhpbbUsers GetAnonymousDbUser()
            => _cache.GetOrAdd(
                key: ANONYMOUS_DB_USER_CACHE_KEY,
                addItemFactory: () => _sqlExecuter.QuerySingle<PhpbbUsers>(
                    "SELECT * FROM phpbb_users WHERE user_id = @ANONYMOUS_USER_ID",
                    new { Constants.ANONYMOUS_USER_ID }),
                expires: DateTimeOffset.UtcNow + CACHE_EXPIRATION);

        public ForumUserExpanded GetAnonymousForumUserExpanded()
            => _cache.GetOrAdd(
                key: ANONYMOUS_FORUM_USER_CACHE_KEY,
                addItemFactory: () =>
                {
                    var dbUser = GetAnonymousDbUser();
                    var expanded = new ForumUserExpanded(DbUserToForumUser(dbUser));
                    expanded.AllPermissions = GetPermissions(expanded.UserId);
                    return expanded;
                },
                expires: DateTimeOffset.UtcNow + CACHE_EXPIRATION);

        public Task<ForumUserExpanded> GetAnonymousForumUserExpandedAsync()
            => _cache.GetOrAddAsync(
                key: ANONYMOUS_FORUM_USER_CACHE_KEY,
                addItemFactory: async () =>
                {
                    var dbUser = await GetAnonymousDbUserAsync();
                    var expanded = new ForumUserExpanded(DbUserToForumUser(dbUser));
                    expanded.AllPermissions = await GetPermissionsAsync(expanded.UserId);
                    return expanded;
                },
                expires: DateTimeOffset.UtcNow + CACHE_EXPIRATION);

        public ForumUser DbUserToForumUser(PhpbbUsers dbUser)
            => new()
            {
                UserId = dbUser.UserId,
                Username = dbUser.Username,
                UsernameClean = dbUser.UsernameClean,
                EmailAddress = dbUser.UserEmail,
                Language = dbUser.UserLang,
                AllowPM = dbUser.UserAllowPm.ToBool(),
                JumpToUnread = dbUser.JumpToUnread,
                UserColor = dbUser.UserColour,
                UserDateFormat = dbUser.UserDateformat,
                ShouldConfirmEmail = dbUser.UserInactiveReason == UserInactiveReason.Active_NotConfirmed
            };

        public async Task<ForumUserExpanded> ExpandForumUser(ForumUser user, ForumUserExpansionType expansionType)
        {
            var sql = new StringBuilder();
            var shouldRunSql = false;
            if (expansionType.HasFlag(ForumUserExpansionType.TopicPostsPerPage))
            {
                shouldRunSql = true;
                sql.AppendLine(
                    @"SELECT DISTINCT topic_id, post_no
	                    FROM phpbb_user_topic_post_number
	                   WHERE user_id = @userId
                       ORDER BY topic_id;");
            }
            if (expansionType.HasFlag(ForumUserExpansionType.Foes))
            {
                shouldRunSql = true;
                sql.AppendLine(
                    @"SELECT zebra_id
                        FROM phpbb_zebra
                       WHERE user_id = @userId 
                         AND foe = 1;");
            }
            if (expansionType.HasFlag(ForumUserExpansionType.UploadLimit))
            {
                shouldRunSql = true;
                sql.AppendLine(
                    @"SELECT g.group_user_upload_size
                        FROM phpbb_groups g
                        JOIN phpbb_users u ON g.group_id = u.group_id
                       WHERE u.user_id = @userId;");
            }
            if (expansionType.HasFlag(ForumUserExpansionType.PostEditTime))
            {
                shouldRunSql = true;
                sql.AppendLine(
                    @"SELECT g.group_edit_time, u.user_edit_time
                        FROM phpbb_groups g
                        JOIN phpbb_users u ON g.group_id = u.group_id
                       WHERE u.user_id = @userId;");
            }
            if (expansionType.HasFlag(ForumUserExpansionType.Style))
            {
                shouldRunSql = true;
                sql.AppendLine(
                    @"SELECT s.style_name 
                        FROM phpbb_styles s
                        JOIN phpbb_users u ON u.user_style = s.style_id
                       WHERE u.user_id = @userId;");
            }

            var expanded = new ForumUserExpanded(user);
            if (expansionType.HasFlag(ForumUserExpansionType.Permissions))
            {
                expanded.AllPermissions = await GetPermissionsAsync(user.UserId);
            }

            SqlMapper.GridReader? result = null;
            try
            {
                if (shouldRunSql)
                {
                    result = await _sqlExecuter.QueryMultipleAsync(sql.ToString(), new { user.UserId });

                    if (expansionType.HasFlag(ForumUserExpansionType.TopicPostsPerPage))
                    {
                        expanded.TopicPostsPerPage = (await result.ReadAsync<(int topicId, int postNo)>()).ToDictionary(x => x.topicId, y => y.postNo);
                    }
                    if (expansionType.HasFlag(ForumUserExpansionType.Foes))
                    {
                        expanded.Foes = new HashSet<int>(await result.ReadAsync<int>());
                    }
                    if (expansionType.HasFlag(ForumUserExpansionType.UploadLimit))
                    {
                        expanded.UploadLimit = await result.ReadFirstOrDefaultAsync<long?>();
                    }
                    if (expansionType.HasFlag(ForumUserExpansionType.PostEditTime))
                    {
                        var (groupEditTime, userEditTime) = await result.ReadFirstOrDefaultAsync<(int groupEditTime, int userEditTime)>();
                        expanded.PostEditTime = (groupEditTime == 0 || userEditTime == 0) ? 0 : Math.Min(Math.Abs(groupEditTime), Math.Abs(userEditTime));
                    }
                    if (expansionType.HasFlag(ForumUserExpansionType.Style))
                    {
                        expanded.Style = await result.ReadFirstOrDefaultAsync<string>();
                    }
                }
            }
            finally
            {
                result?.Dispose();
            }

            return expanded;
        }

        public async Task<IEnumerable<PhpbbRanks>> GetAllRanks()
            => await _sqlExecuter.QueryAsync<PhpbbRanks>("SELECT * FROM phpbb_ranks ORDER BY rank_title");

        public async Task<IEnumerable<PhpbbGroups>> GetAllGroups()
        {

            return await _sqlExecuter.QueryAsync<PhpbbGroups>("SELECT * FROM phpbb_groups ORDER BY group_name");
        }

        public async Task<PhpbbGroups> GetUserGroup(int userId)
        {
            return await _sqlExecuter.QueryFirstOrDefaultAsync<PhpbbGroups>(
                @"SELECT g.* FROM phpbb_groups g 
                    JOIN phpbb_user_group ug ON g.group_id = ug.group_id 
                   WHERE ug.user_id = @userId",
                new { userId }
            );
        }

        public async Task<ForumUser> GetForumUserById(int userId, ITransactionalSqlExecuter? transaction = null)
        {
            var usr = await (transaction ?? _sqlExecuter).QuerySingleOrDefaultAsync<PhpbbUsers>("SELECT * FROM phpbb_users WHERE user_id = @userId", new { userId });
            return DbUserToForumUser(usr);
        }

        public async Task<(string Message, bool? IsSuccess)> SendPrivateMessage(ForumUserExpanded sender, int receiverId, string subject, string text)
        {
            var language = _translationProvider.GetLanguage();
            try
            {
                if (sender.UserId == receiverId)
                {
                    return (_translationProvider.Errors[language, "NOT_ALLOWED"], false);
                }

                if (!sender.HasPrivateMessagePermissions)
                {
                    return (_translationProvider.Errors[language, "SENDER_CANT_SEND_PMS"], false);
                }

                var receiver = await ExpandForumUser(await GetForumUserById(receiverId), ForumUserExpansionType.Permissions | ForumUserExpansionType.Foes);
                var senderIsGlobalModerator = await IsUserModeratorInForum(sender, forumId: 0);
                if (!receiver.HasPrivateMessages && !senderIsGlobalModerator)
                {
                    return (_translationProvider.Errors[language, "RECEIVER_CANT_RECEIVE_PMS"], false);
                }
                if (receiver.Foes?.Contains(sender.UserId) == true && !senderIsGlobalModerator)
                {
                    return (_translationProvider.Errors[language, "ON_RECEIVERS_FOE_LIST"], false);
                }

                await _sqlExecuter.CallStoredProcedureAsync("save_new_private_message",
                    new
                    {
                        senderId = sender.UserId,
                        receiverId,
                        subject,
                        text,
                        time = DateTime.UtcNow.ToUnixTimestamp()
                    });

                var emailSubject = string.Format(_translationProvider.Email[receiver.Language!, "NEWPM_SUBJECT_FORMAT"], _config.GetValue<string>("ForumName"));
                await _emailService.SendEmail(
                    to: receiver.EmailAddress!,
                    subject: emailSubject,
                    bodyRazorViewName: "_NewPMEmailPartial",
                    bodyRazorViewModel: new NewPMEmailDto(sender.Username!, receiver.Language));

                return ("OK", true);
            }
            catch (Exception ex)
            {
                _logger.Error(ex);
                return (_translationProvider.Errors[language, "AN_ERROR_OCCURRED_TRY_AGAIN"], false);
            }
        }

        public async Task<(string Message, bool? IsSuccess)> EditPrivateMessage(int messageId, string subject, string text)
        {
            var language = _translationProvider.GetLanguage();
            try
            {
                var rows = await _sqlExecuter.ExecuteAsync(
                    @"UPDATE phpbb_privmsgs SET message_subject = @subject, message_text = @text 
                       WHERE msg_id = @messageId 
                         AND EXISTS(
                                SELECT 1 
                                  FROM phpbb_privmsgs_to 
                                 WHERE msg_id = @messageId 
                                   AND pm_unread = 1 
                                   AND user_id <> author_id)",
                    new { subject, text, messageId });

                if (rows == 0)
                {
                    return (_translationProvider.Errors[language, "CANT_EDIT_ALREADY_READ"], false);
                }

                return ("OK", true);
            }
            catch (Exception ex)
            {
                _logger.Error(ex);
                return (_translationProvider.Errors[language, "AN_ERROR_OCCURRED_TRY_AGAIN"], false);
            }
        }

        public async Task<(string Message, bool? IsSuccess)> DeletePrivateMessage(int messageId)
        {
            var language = _translationProvider.GetLanguage();
            try
            {
                using var transaction = _sqlExecuter.BeginTransaction();
                var rows = await transaction.ExecuteAsync(
                    @"DELETE m FROM phpbb_privmsgs m JOIN phpbb_privmsgs_to tt ON m.msg_id = tt.msg_id AND tt.pm_unread = 1 WHERE m.msg_id = @messageId; 
                      DELETE t FROM phpbb_privmsgs_to t JOIN phpbb_privmsgs_to tt ON t.msg_id = tt.msg_id AND tt.pm_unread = 1 WHERE t.msg_id = @messageId;",
                    new { messageId });
                transaction.CommitTransaction();

                if (rows == 0)
                {
                    return (_translationProvider.Errors[language, "CANT_DELETE_ALREADY_READ_OR_NONEXISTING"], false);
                }

                return ("OK", true);
            }
            catch (Exception ex)
            {
                _logger.Error(ex);
                return (_translationProvider.Errors[language, "AN_ERROR_OCCURRED_TRY_AGAIN"], false);
            }
        }

        public async Task<(string Message, bool? IsSuccess)> HidePrivateMessages(int userId, params int[] messageIds)
        {
            var language = _translationProvider.GetLanguage();
            try
            {

                var rows = await _sqlExecuter.ExecuteAsync(
                    @"UPDATE phpbb_privmsgs_to
                         SET folder_id = -10
                       WHERE msg_id IN @messageIds AND user_id = @userId",
                    new { messageIds = messageIds.DefaultIfEmpty(), userId }
                );
                if (rows < messageIds.Length)
                {
                    return (string.Format(_translationProvider.Errors[language, "SOME_MESSAGES_MISSING_FORMAT"], rows, messageIds.Length - rows), false);
                }
                return ("OK", true);
            }
            catch (Exception ex)
            {
                _logger.Error(ex);
                return (_translationProvider.Errors[language, "AN_ERROR_OCCURRED_TRY_AGAIN"], false);
            }
        }

        public Task<int> GetUnreadPMCount(int userId)
            => _sqlExecuter.QueryFirstOrDefaultAsync<int>(
                    "SELECT count(1) FROM phpbb_privmsgs_to WHERE user_id = @userId AND folder_id >= 0 AND pm_unread = 1",
                    new { userId });

        HashSet<ForumUserExpanded.Permissions> GetPermissions(int userId)
            => _permissionsMap.GetOrAdd(userId, id => new(_sqlExecuter.CallStoredProcedure<ForumUserExpanded.Permissions>("get_user_permissions", new { id })));

        Task<HashSet<ForumUserExpanded.Permissions>> GetPermissionsAsync(int userId)
            => Task.FromResult(GetPermissions(userId));

        public Task<IEnumerable<PhpbbAclRoles>> GetUserRolesLazy()
            => _cache.GetOrAddAsync(
                    key: USER_ROLES_CACHE_KEY,
                    addItemFactory: () => _sqlExecuter.QueryAsync<PhpbbAclRoles>("SELECT * FROM phpbb_acl_roles WHERE role_type = 'u_'"),
                    expires: DateTime.UtcNow + CACHE_EXPIRATION);

        public async Task<List<KeyValuePair<string, int>>> GetUserMap()
        {
            if (_userMap is not null)
            {
                return _userMap;
            }
            _userMap = (
                await _sqlExecuter.QueryAsync<(string username, int userId)>("SELECT username, user_id FROM phpbb_users WHERE user_id <> @id AND user_type <> 2 ORDER BY username", new { id = Constants.ANONYMOUS_USER_ID })
            ).Select(u => KeyValuePair.Create(u.username, u.userId)).ToList();
            return _userMap;
        }

        public async Task<IEnumerable<KeyValuePair<string, string>>> GetUsers()
            => (await GetUserMap()).Select(map => KeyValuePair.Create(map.Key, $"[url={_config.GetValue<string>("BaseUrl").TrimEnd('/')}/User?UserId={map.Value}]{map.Key}[/url]")).ToList();

        private Task<IEnumerable<PhpbbAclRoles>> GetModRolesLazy()
            => _cache.GetOrAddAsync(
                    key: MOD_ROLES_CACHE_KEY,
                    addItemFactory: () => _sqlExecuter.QueryAsync<PhpbbAclRoles>("SELECT * FROM phpbb_acl_roles WHERE role_type = 'm_'"),
                    expires: DateTime.UtcNow + CACHE_EXPIRATION);

        private Task<IEnumerable<PhpbbAclRoles>> GetAdminRolesLazy()
            => _cache.GetOrAddAsync(
                    key: ADMIN_ROLES_CACHE_KEY,
                    addItemFactory: () => _sqlExecuter.QueryAsync<PhpbbAclRoles>("SELECT * FROM phpbb_acl_roles WHERE role_type = 'a_'"),
                    expires: DateTime.UtcNow + CACHE_EXPIRATION);
    }
}
