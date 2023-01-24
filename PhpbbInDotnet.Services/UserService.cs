using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Configuration;
using PhpbbInDotnet.Database;
using PhpbbInDotnet.Database.Entities;
using PhpbbInDotnet.Domain;
using PhpbbInDotnet.Domain.Extensions;
using PhpbbInDotnet.Languages;
using PhpbbInDotnet.Objects;
using Serilog;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
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

        private static PhpbbUsers? _anonymousDbUser;

        public UserService(ISqlExecuter sqlExecuter, IConfiguration config, ITranslationProvider translationProvider, ILogger logger, IEmailService emailService)
        {
            _translationProvider = translationProvider;
            _sqlExecuter = sqlExecuter;
            _config = config;
            _logger = logger;
            _emailService = emailService;
        }

        public async Task<bool> IsAdmin(AuthenticatedUserExpanded user)
            => (from up in user.AllPermissions ?? new HashSet<AuthenticatedUserExpanded.Permissions>()
                where up.ForumId == 0
                join a in await GetAdminRolesLazy()
                on up.AuthRoleId equals a.RoleId
                select up).Any();

        public async Task<bool> IsUserModeratorInForum(AuthenticatedUserExpanded user, int forumId)
            => await IsAdmin(user) ||
                (from up in user.AllPermissions ?? new HashSet<AuthenticatedUserExpanded.Permissions>()
                 where up.ForumId == forumId || up.ForumId == 0
                 join a in await GetModRolesLazy()
                 on up.AuthRoleId equals a.RoleId
                 select up).Any();

        public async Task<int?> GetUserRole(AuthenticatedUserExpanded user)
            => (from up in user.AllPermissions ?? new HashSet<AuthenticatedUserExpanded.Permissions>()
                join a in await GetUserRolesLazy()
                on up.AuthRoleId equals a.RoleId
                select up.AuthRoleId as int?).FirstOrDefault();

        public async Task<PhpbbUsers> GetAnonymousDbUser()
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

        public AuthenticatedUser DbUserToAuthenticatedUserBase(PhpbbUsers dbUser)
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

        public async Task<IEnumerable<PhpbbRanks>> GetAllRanks()
        {
            
            return await _sqlExecuter.QueryAsync<PhpbbRanks>("SELECT * FROM phpbb_ranks ORDER BY rank_title");
        }

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

        public async Task<AuthenticatedUser> GetAuthenticatedUserById(int userId)
        {
            var usr = await _sqlExecuter.QuerySingleOrDefaultAsync<PhpbbUsers>("SELECT * FROM phpbb_users WHERE user_id = @userId", new { userId });
            return DbUserToAuthenticatedUserBase(usr);
        }

        public async Task<(string Message, bool? IsSuccess)> SendPrivateMessage(AuthenticatedUserExpanded sender, int receiverId, string subject, string text, PageContext pageContext, HttpContext httpContext)
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

                var receiver = new AuthenticatedUserExpanded(await GetAuthenticatedUserById(receiverId))
                {
                    AllPermissions = await GetPermissions(receiverId),
                    Foes = await GetFoes(receiverId)
                };
                if (!receiver.HasPrivateMessages)
                {
                    return (_translationProvider.Errors[language, "RECEIVER_CANT_RECEIVE_PMS"], false);
                }
                if (receiver.Foes?.Contains(sender.UserId) == true)
                {
                    return (_translationProvider.Errors[language, "ON_RECEIVERS_FOE_LIST"], false);
                }

                await _sqlExecuter.ExecuteAsync(
                    @"START TRANSACTION;
                      INSERT INTO phpbb_privmsgs (author_id, to_address, bcc_address, message_subject, message_text, message_time) VALUES (@senderId, @to, '', @subject, @text, @time); 
                      SELECT LAST_INSERT_ID() INTO @inserted_id;
                      INSERT INTO phpbb_privmsgs_to (author_id, msg_id, user_id, folder_id, pm_unread) VALUES (@senderId, @inserted_id, @receiverId, 0, 1); 
                      INSERT INTO phpbb_privmsgs_to (author_id, msg_id, user_id, folder_id, pm_unread) VALUES (@senderId, @inserted_id, @senderId, -1, 0);
                      COMMIT;",
                    new 
                    { 
                        senderId = sender.UserId, 
                        receiverId, 
                        to = $"u_{receiverId}", 
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

        public async Task<HashSet<AuthenticatedUserExpanded.Permissions>> GetPermissions(int userId)
            => new HashSet<AuthenticatedUserExpanded.Permissions>(
                    await _sqlExecuter.QueryAsync<AuthenticatedUserExpanded.Permissions>("CALL get_user_permissions(@userId)", new { userId }));

        public async Task<HashSet<int>> GetFoes(int userId)
            => new HashSet<int>(await _sqlExecuter.QueryAsync<int>(
                    @"SELECT zebra_id
                        FROM phpbb_zebra
                       WHERE user_id = @user_id 
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
