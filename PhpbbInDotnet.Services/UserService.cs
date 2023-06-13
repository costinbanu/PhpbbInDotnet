using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Org.BouncyCastle.Asn1.X509;
using PhpbbInDotnet.Database.Entities;
using PhpbbInDotnet.Database.SqlExecuter;
using PhpbbInDotnet.Domain;
using PhpbbInDotnet.Domain.Extensions;
using PhpbbInDotnet.Languages;
using PhpbbInDotnet.Objects;
using Serilog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection.Metadata.Ecma335;
using System.Threading.Tasks;

namespace PhpbbInDotnet.Services
{
    class UserService : IUserService
    {
        private readonly ISqlExecuter _sqlExecuter;
        private readonly IConfiguration _config;
        private readonly ILogger _logger;
        private readonly IEmailService _emailService;
        private readonly ITranslationProvider _translationProvider;

        private IEnumerable<PhpbbAclRoles>? _adminRoles;
        private IEnumerable<PhpbbAclRoles>? _modRoles;
        private IEnumerable<PhpbbAclRoles>? _userRoles;
        private List<KeyValuePair<string, int>>? _userMap;

        private PhpbbUsers? _anonymousDbUser;
        private ConcurrentDictionary<int, HashSet<ForumUserExpanded.Permissions>> _permissionsMap = new();

        public UserService(ISqlExecuter sqlExecuter, IConfiguration config, ITranslationProvider translationProvider, ILogger logger, IEmailService emailService)
        {
            _translationProvider = translationProvider;
            _sqlExecuter = sqlExecuter;
            _config = config;
            _logger = logger;
            _emailService = emailService;
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

        public async Task<PhpbbUsers> GetAnonymousDbUserAsync()
        {
            if (_anonymousDbUser != null)
            {
                return _anonymousDbUser;
            }

            _anonymousDbUser = await _sqlExecuter.QuerySingleAsync<PhpbbUsers>(
                "SELECT * FROM phpbb_users WHERE user_id = @ANONYMOUS_USER_ID",
                new { Constants.ANONYMOUS_USER_ID });
            return _anonymousDbUser;
        }

        PhpbbUsers GetAnonymousDbUser()
        {
            if (_anonymousDbUser != null)
            {
                return _anonymousDbUser;
            }

            _anonymousDbUser = _sqlExecuter.QuerySingle<PhpbbUsers>(
                "SELECT * FROM phpbb_users WHERE user_id = @ANONYMOUS_USER_ID",
                new { Constants.ANONYMOUS_USER_ID });
            return _anonymousDbUser;
        }

        public ForumUserExpanded GetAnonymousForumUserExpanded()
        {
            var dbUser = GetAnonymousDbUser();
            var expanded = new ForumUserExpanded(DbUserToForumUser(dbUser));
            expanded.AllPermissions = GetPermissions(expanded.UserId);
            return expanded;
        }

        public async Task<ForumUserExpanded> GetAnonymousForumUserExpandedAsync()
        {
            var dbUser = await GetAnonymousDbUserAsync();
            var expanded = new ForumUserExpanded(DbUserToForumUser(dbUser));
            expanded.AllPermissions = await GetPermissionsAsync(expanded.UserId);
            return expanded;
        }

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
            var expanded = new ForumUserExpanded(user)
            {
                AllPermissions = await ValueIfTrue(expansionType.HasFlag(ForumUserExpansionType.Permissions), GetPermissionsAsync(user.UserId)),
                TopicPostsPerPage = await ValueIfTrue(expansionType.HasFlag(ForumUserExpansionType.TopicPostsPerPage), GetTopicPostsPage(user.UserId)),
                Foes = await ValueIfTrue(expansionType.HasFlag(ForumUserExpansionType.Foes), GetFoes(user.UserId)),
                UploadLimit = await ValueIfTrue(expansionType.HasFlag(ForumUserExpansionType.UploadLimit), GetUploadLimit(user.UserId)),
                PostEditTime = await ValueIfTrue(expansionType.HasFlag(ForumUserExpansionType.PostEditTime), GetPostEditTime(user.UserId)),
                Style = await ValueIfTrue(expansionType.HasFlag(ForumUserExpansionType.Style), GetStyle(user.UserId), string.Empty)
            };

            return expanded;
        }

        static Task<T> ValueIfTrue<T>(bool condition, Task<T> factory, T defaultValue)
            => condition ? factory : Task.FromResult(defaultValue);

        static Task<T> ValueIfTrue<T>(bool condition, Task<T> factory) where T : new()
            => ValueIfTrue(condition, factory, new());

        Task<string> GetStyle(int userId)
            => _sqlExecuter.QueryFirstOrDefaultAsync<string>(
                @"SELECT s.style_name 
                    FROM phpbb_styles s
                    JOIN phpbb_users u ON u.user_style = s.style_id
                   WHERE u.user_id = @userId",
                new { userId });

        async Task<int> GetPostEditTime(int userId)
        {
            var (groupEditTime, userEditTime) = await _sqlExecuter.QueryFirstOrDefaultAsync<(int groupEditTime, int userEditTime)>(
                @"SELECT g.group_edit_time, u.user_edit_time
                    FROM phpbb_groups g
                    JOIN phpbb_users u ON g.group_id = u.group_id
                   WHERE u.user_id = @userId",
                new { userId });
            return (groupEditTime == 0 || userEditTime == 0) ? 0 : Math.Min(Math.Abs(groupEditTime), Math.Abs(userEditTime));
        }

        Task<int> GetUploadLimit(int userId)
            => _sqlExecuter.QueryFirstOrDefaultAsync<int>(
                    @"SELECT g.group_user_upload_size
                        FROM phpbb_groups g
                        JOIN phpbb_users u ON g.group_id = u.group_id
                       WHERE u.user_id = @userId",
                    new { userId });

        async Task<Dictionary<int, int>> GetTopicPostsPage(int userId)
        {
            var results = await _sqlExecuter.QueryAsync<(int topicId, int postNo)>(
                @"SELECT DISTINCT topic_id, post_no
	                FROM phpbb_user_topic_post_number
	               WHERE user_id = @userId
                   ORDER BY topic_id;",
                new { userId });
            return results.ToDictionary(x => x.topicId, y => y.postNo);
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

        public async Task<ForumUser> GetForumUserById(int userId)
        {
            var usr = await _sqlExecuter.QuerySingleOrDefaultAsync<PhpbbUsers>("SELECT * FROM phpbb_users WHERE user_id = @userId", new { userId });
            return DbUserToForumUser(usr);
        }

        public async Task<(string Message, bool? IsSuccess)> SendPrivateMessage(ForumUserExpanded sender, int receiverId, string subject, string text, PageContext pageContext, HttpContext httpContext)
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
                if (!receiver.HasPrivateMessages)
                {
                    return (_translationProvider.Errors[language, "RECEIVER_CANT_RECEIVE_PMS"], false);
                }
                if (receiver.Foes?.Contains(sender.UserId) == true)
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
                var rows = await _sqlExecuter.ExecuteAsync(
                    @"START TRANSACTION;
                      DELETE m FROM phpbb_privmsgs m JOIN phpbb_privmsgs_to tt ON m.msg_id = tt.msg_id AND tt.pm_unread = 1 WHERE m.msg_id = @messageId; 
                      DELETE t FROM phpbb_privmsgs_to t JOIN phpbb_privmsgs_to tt ON t.msg_id = tt.msg_id AND tt.pm_unread = 1 WHERE t.msg_id = @messageId;
                      COMMIT;",
                    new { messageId });

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

        async Task<HashSet<int>> GetFoes(int userId)
            => new HashSet<int>(await _sqlExecuter.QueryAsync<int>(
                    @"SELECT zebra_id
                        FROM phpbb_zebra
                       WHERE user_id = @userId 
                         AND foe = 1;",
                    new { userId }
                ));

        public async Task<IEnumerable<PhpbbAclRoles>> GetUserRolesLazy()
        {
            if (_userRoles != null)
            {
                return _userRoles;
            }

            _userRoles = await _sqlExecuter.QueryAsync<PhpbbAclRoles>("SELECT * FROM phpbb_acl_roles WHERE role_type = 'u_'");

            return _userRoles;
        }

        public async Task<List<KeyValuePair<string, int>>> GetUserMap()
        {
            if (_userMap is not null)
            {
                return _userMap;
            }
            _userMap = (
                await _sqlExecuter.QueryAsync("SELECT username, user_id FROM phpbb_users WHERE user_id <> @id AND user_type <> 2 ORDER BY username", new { id = Constants.ANONYMOUS_USER_ID })
            ).Select(u => KeyValuePair.Create((string)u.username, (int)u.user_id)).ToList();
            return _userMap;
        }

        public async Task<IEnumerable<KeyValuePair<string, string>>> GetUsers()
            => (await GetUserMap()).Select(map => KeyValuePair.Create(map.Key, $"[url={_config.GetValue<string>("BaseUrl").TrimEnd('/')}/User?UserId={map.Value}]{map.Key}[/url]")).ToList();

        private async Task<IEnumerable<PhpbbAclRoles>> GetModRolesLazy()
        {
            if (_modRoles != null)
            {
                return _modRoles;
            }

            _modRoles = await _sqlExecuter.QueryAsync<PhpbbAclRoles>("SELECT * FROM phpbb_acl_roles WHERE role_type = 'm_'");

            return _modRoles;
        }

        private async Task<IEnumerable<PhpbbAclRoles>> GetAdminRolesLazy()
        {
            if (_adminRoles != null)
            {
                return _adminRoles;
            }

            _adminRoles = await _sqlExecuter.QueryAsync<PhpbbAclRoles>("SELECT * FROM phpbb_acl_roles WHERE role_type = 'a_'");

            return _adminRoles;
        }
    }
}
