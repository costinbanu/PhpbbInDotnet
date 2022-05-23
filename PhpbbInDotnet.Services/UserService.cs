using Dapper;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using PhpbbInDotnet.Database;
using PhpbbInDotnet.Database.Entities;
using PhpbbInDotnet.Languages;
using PhpbbInDotnet.Objects;
using PhpbbInDotnet.Utilities;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace PhpbbInDotnet.Services
{
    class UserService : MultilingualServiceBase, IUserService
    {
        private readonly IForumDbContext _context;
        private readonly IConfiguration _config;
        private IEnumerable<PhpbbAclRoles>? _adminRoles;
        private IEnumerable<PhpbbAclRoles>? _modRoles;
        private IEnumerable<PhpbbAclRoles>? _userRoles;

        private static PhpbbUsers? _anonymousDbUser;
        private static ClaimsPrincipal? _anonymousClaimsPrincipal;

        public UserService(ICommonUtils utils, IForumDbContext context, IConfiguration config, LanguageProvider languageProvider, IHttpContextAccessor httpContextAccessor)
            : base(utils, languageProvider, httpContextAccessor)
        {
            _context = context;
            _config = config;
        }

        public async Task<bool> IsUserAdminInForum(AuthenticatedUserExpanded? user, int forumId)
            => user != null && (
                from up in user.AllPermissions ?? new HashSet<AuthenticatedUserExpanded.Permissions>()
                where up.ForumId == forumId || up.ForumId == 0
                join a in await GetAdminRolesLazy()
                on up.AuthRoleId equals a.RoleId
                select up
            ).Any();

        public async Task<bool> IsUserModeratorInForum(AuthenticatedUserExpanded? user, int forumId)
            => (await IsUserAdminInForum(user, forumId)) || (
                user != null && (
                    from up in user.AllPermissions ?? new HashSet<AuthenticatedUserExpanded.Permissions>()
                    where up.ForumId == forumId || up.ForumId == 0
                    join a in await GetModRolesLazy()
                    on up.AuthRoleId equals a.RoleId
                    select up
                ).Any()
            );

        public async Task<int?> GetUserRole(AuthenticatedUserExpanded user)
            => (from up in user.AllPermissions ?? new HashSet<AuthenticatedUserExpanded.Permissions>()
                join a in await GetUserRolesLazy()
                on up.AuthRoleId equals a.RoleId
                select up.AuthRoleId as int?).FirstOrDefault();

        public async Task<bool> HasPrivateMessagePermissions(int userId)
        {
            var usr = new AuthenticatedUserExpanded(await GetAuthenticatedUserById(userId));
            usr.AllPermissions = await GetPermissions(userId);
            return usr.HasPrivateMessagePermissions;
        }

        public async Task<PhpbbUsers> GetAnonymousDbUser()
        {
            if (_anonymousDbUser != null)
            {
                return _anonymousDbUser;
            }
            var connection = _context.GetDbConnection();
            _anonymousDbUser = await connection.QuerySingleOrDefaultAsync<PhpbbUsers>("SELECT * FROM phpbb_users WHERE user_id = @userId", new { userId = Constants.ANONYMOUS_USER_ID });
            return _anonymousDbUser;
        }

        public async Task<ClaimsPrincipal> GetAnonymousClaimsPrincipal()
        {
            if (_anonymousClaimsPrincipal != null)
            {
                return _anonymousClaimsPrincipal;
            }
            _anonymousClaimsPrincipal = await DbUserToClaimsPrincipal(await GetAnonymousDbUser());
            return _anonymousClaimsPrincipal;
        }

        public async Task<ClaimsPrincipal> DbUserToClaimsPrincipal(PhpbbUsers user)
        {
            var connection = _context.GetDbConnection();
            var groupPropertiesTask = connection.QueryFirstOrDefaultAsync(
                @"SELECT g.group_edit_time, g.group_user_upload_size
                    FROM phpbb_groups g
                    JOIN phpbb_users u ON g.group_id = u.group_id
                   WHERE u.user_id = @UserId",
                new { user.UserId }
            );
            var styleTask = connection.QueryFirstOrDefaultAsync<string>(
                @"SELECT style_name 
                    FROM phpbb_styles 
                   WHERE style_id = @UserStyle",
                new { user.UserStyle }
            );
            await Task.WhenAll(styleTask, groupPropertiesTask);
            var groupProperties = await groupPropertiesTask;
            var editTime = unchecked((int)groupProperties.group_edit_time);
            var style = await styleTask;

            var identity = new ClaimsIdentity(CookieAuthenticationDefaults.AuthenticationScheme);
            identity.AddClaim(new Claim(nameof(AuthenticatedUserExpanded.UserId), user.UserId.ToString()));
            identity.AddClaim(new Claim(nameof(AuthenticatedUserExpanded.Username), user.Username ?? string.Empty));
            identity.AddClaim(new Claim(nameof(AuthenticatedUserExpanded.UsernameClean), user.UsernameClean ?? string.Empty));
            identity.AddClaim(new Claim(nameof(AuthenticatedUserExpanded.UserDateFormat), user.UserDateformat ?? string.Empty));
            identity.AddClaim(new Claim(nameof(AuthenticatedUserExpanded.UserColor), user.UserColour ?? string.Empty));
            identity.AddClaim(new Claim(nameof(AuthenticatedUserExpanded.PostEditTime), ((editTime == 0 || user.UserEditTime == 0) ? 0 : Math.Min(Math.Abs(editTime), Math.Abs(user.UserEditTime))).ToString()));
            identity.AddClaim(new Claim(nameof(AuthenticatedUserExpanded.UploadLimit), unchecked((int)groupProperties.group_user_upload_size).ToString()));
            identity.AddClaim(new Claim(nameof(AuthenticatedUserExpanded.AllowPM), user.UserAllowPm.ToBool().ToString()));
            identity.AddClaim(new Claim(nameof(AuthenticatedUserExpanded.Style), style ?? string.Empty));
            identity.AddClaim(new Claim(nameof(AuthenticatedUserExpanded.JumpToUnread), user.JumpToUnread?.ToString() ?? string.Empty));
            identity.AddClaim(new Claim(nameof(AuthenticatedUserExpanded.Language), user.UserLang ?? string.Empty));
            identity.AddClaim(new Claim(nameof(AuthenticatedUserExpanded.EmailAddress), user.UserEmail ?? string.Empty));
            return new ClaimsPrincipal(identity);
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
                UserDateFormat = dbUser.UserDateformat
            };

        public AuthenticatedUserExpanded? ClaimsPrincipalToAuthenticatedUser(ClaimsPrincipal claimsPrincipal)
        {
            var user = new AuthenticatedUserExpanded();
            var found = false;
            foreach (var claim in claimsPrincipal?.Claims ?? Enumerable.Empty<Claim>())
            {
                switch (claim.Type)
                {
                    case nameof(AuthenticatedUserExpanded.UserId):
                        user.UserId = int.Parse(claim.Value);
                        found = true;
                        break;
                    case nameof(AuthenticatedUserExpanded.Username):
                        user.Username = claim.Value;
                        found = true;
                        break;
                    case nameof(AuthenticatedUserExpanded.UsernameClean):
                        user.UsernameClean = claim.Value;
                        found = true;
                        break;
                    case nameof(AuthenticatedUserExpanded.UserDateFormat):
                        user.UserDateFormat = claim.Value;
                        found = true;
                        break;
                    case nameof(AuthenticatedUserExpanded.UserColor):
                        user.UserColor = claim.Value;
                        found = true;
                        break;
                    case nameof(AuthenticatedUserExpanded.PostEditTime):
                        user.PostEditTime = int.Parse(claim.Value);
                        found = true;
                        break;
                    case nameof(AuthenticatedUserExpanded.UploadLimit):
                        user.UploadLimit = int.Parse(claim.Value);
                        found = true;
                        break;
                    case nameof(AuthenticatedUserExpanded.AllowPM):
                        user.AllowPM = bool.TryParse(claim.Value, out var val) && val;
                        found = true;
                        break;
                    case nameof(AuthenticatedUserExpanded.Style):
                        user.Style = claim.Value;
                        found = true;
                        break;
                    case nameof(AuthenticatedUserExpanded.JumpToUnread):
                        user.JumpToUnread = bool.TryParse(claim.Value, out val) && val;
                        found = true;
                        break;
                    case nameof(AuthenticatedUserExpanded.Language):
                        user.Language = claim.Value;
                        found = true;
                        break;
                    case nameof(AuthenticatedUserExpanded.EmailAddress):
                        user.EmailAddress = claim.Value;
                        found = true;
                        break;
                }
            }
            return found ? user : null;
        }

        public async Task<IEnumerable<PhpbbRanks>> GetRankList()
        {
            var connection = _context.GetDbConnection();
            return await connection.QueryAsync<PhpbbRanks>("SELECT * FROM phpbb_ranks ORDER BY rank_title");
        }

        public async Task<IEnumerable<PhpbbGroups>> GetGroupList()
        {
            var connection = _context.GetDbConnection();
            return await connection.QueryAsync<PhpbbGroups>("SELECT * FROM phpbb_groups ORDER BY group_name");
        }

        public async Task<PhpbbGroups> GetUserGroup(int userId)
        {
            var connection = _context.GetDbConnection();
            return await connection.QueryFirstOrDefaultAsync<PhpbbGroups>(
                "SELECT g.* FROM phpbb_groups g " +
                "JOIN phpbb_user_group ug on g.group_id = ug.group_id " +
                "WHERE ug.user_id = @userId",
                new { userId }
            );
        }

        public async Task<AuthenticatedUser> GetAuthenticatedUserById(int userId)
        {
            var connection = _context.GetDbConnection();
            var usr = await connection.QuerySingleOrDefaultAsync<PhpbbUsers>("SELECT * FROM phpbb_users WHERE user_id = @userId", new { userId });
            return DbUserToAuthenticatedUserBase(usr);
        }

        public async Task<(string Message, bool? IsSuccess)> SendPrivateMessage(int senderId, string senderName, int receiverId, string subject, string text, PageContext pageContext, HttpContext httpContext)
        {
            var lang = GetLanguage();
            try
            {
                var sender = new AuthenticatedUserExpanded(await GetAuthenticatedUserById(senderId))
                {
                    AllPermissions = await GetPermissions(senderId),
                };
                if (!sender.HasPrivateMessagePermissions)
                {
                    return (LanguageProvider.Errors[lang, "SENDER_CANT_SEND_PMS"], false);
                }

                var receiver = new AuthenticatedUserExpanded(await GetAuthenticatedUserById(receiverId))
                {
                    AllPermissions = await GetPermissions(receiverId),
                    Foes = await GetFoes(receiverId)
                };
                if (!receiver.HasPrivateMessages)
                {
                    return (LanguageProvider.Errors[lang, "RECEIVER_CANT_RECEIVE_PMS"], false);
                }
                if (receiver.Foes?.Contains(senderId) ?? false)
                {
                    return (LanguageProvider.Errors[lang, "ON_RECEIVERS_FOE_LIST"], false);
                }

                var connection = _context.GetDbConnection();

                await connection.ExecuteAsync(
                    @"INSERT INTO phpbb_privmsgs (author_id, to_address, bcc_address, message_subject, message_text, message_time) VALUES (@senderId, @to, '', @subject, @text, @time); 
                      SELECT LAST_INSERT_ID() INTO @inserted_id;
                      INSERT INTO phpbb_privmsgs_to (author_id, msg_id, user_id, folder_id, pm_unread) VALUES (@senderId, @inserted_id, @receiverId, 0, 1); 
                      INSERT INTO phpbb_privmsgs_to (author_id, msg_id, user_id, folder_id, pm_unread) VALUES (@senderId, @inserted_id, @senderId, -1, 0); ",
                    new { senderId, receiverId, to = $"u_{receiverId}", subject, text, time = DateTime.UtcNow.ToUnixTimestamp() }
                );

                var emailSubject = string.Format(LanguageProvider.Email[receiver.Language!, "NEWPM_SUBJECT_FORMAT"], _config.GetValue<string>("ForumName"));
                await Utils.SendEmail(
                    to: receiver.EmailAddress!,
                    subject: emailSubject,
                    body: await Utils.RenderRazorViewToString(
                        "_NewPMEmailPartial",
                        new NewPMEmailDto
                        {
                            SenderName = senderName,
                            Language = receiver.Language!
                        },
                        pageContext,
                        httpContext
                    ));

                return ("OK", true);
            }
            catch (Exception ex)
            {
                Utils.HandleError(ex);
                return (LanguageProvider.Errors[lang, "AN_ERROR_OCCURRED_TRY_AGAIN"], false);
            }
        }

        public async Task<(string Message, bool? IsSuccess)> EditPrivateMessage(int messageId, string subject, string text)
        {
            var lang = GetLanguage();
            try
            {
                var connection = _context.GetDbConnection();

                var rows = await connection.ExecuteAsync(
                    "UPDATE phpbb_privmsgs SET message_subject = @subject, message_text = @text " +
                    "WHERE msg_id = @messageId AND EXISTS(SELECT 1 FROM phpbb_privmsgs_to WHERE msg_id = @messageId AND pm_unread = 1 AND user_id <> author_id)",
                    new { subject, text, messageId }
                );

                if (rows == 0)
                {
                    return (LanguageProvider.Errors[lang, "CANT_EDIT_ALREADY_READ"], false);
                }

                return ("OK", true);
            }
            catch (Exception ex)
            {
                Utils.HandleError(ex);
                return (LanguageProvider.Errors[lang, "AN_ERROR_OCCURRED_TRY_AGAIN"], false);
            }
        }

        public async Task<(string Message, bool? IsSuccess)> DeletePrivateMessage(int messageId)
        {
            var lang = GetLanguage();
            try
            {
                var connection = _context.GetDbConnection();

                var rows = await connection.ExecuteAsync(
                    "DELETE m FROM phpbb_privmsgs m JOIN phpbb_privmsgs_to tt ON m.msg_id = tt.msg_id AND tt.pm_unread = 1 WHERE m.msg_id = @messageId; " +
                    "DELETE t FROM phpbb_privmsgs_to t JOIN phpbb_privmsgs_to tt ON t.msg_id = tt.msg_id AND tt.pm_unread = 1 WHERE t.msg_id = @messageId;",
                    new { messageId }
                );

                if (rows == 0)
                {
                    return (LanguageProvider.Errors[lang, "CANT_DELETE_ALREADY_READ_OR_NONEXISTING"], false);
                }

                return ("OK", true);
            }
            catch (Exception ex)
            {
                Utils.HandleError(ex);
                return (LanguageProvider.Errors[lang, "AN_ERROR_OCCURRED_TRY_AGAIN"], false);
            }
        }

        public async Task<(string Message, bool? IsSuccess)> HidePrivateMessages(int userId, params int[] messageIds)
        {
            var lang = GetLanguage();
            try
            {
                var connection = _context.GetDbConnection();
                var rows = await connection.ExecuteAsync(
                    @"UPDATE phpbb_privmsgs_to
                         SET folder_id = -10
                       WHERE msg_id IN @messageIds AND user_id = @userId",
                    new { messageIds, userId }
                );
                if (rows < messageIds.Length)
                {
                    return (string.Format(LanguageProvider.Errors[lang, "SOME_MESSAGES_MISSING_FORMAT"], rows, messageIds.Length - rows), false);
                }
                return ("OK", true);
            }
            catch (Exception ex)
            {
                Utils.HandleError(ex);
                return (LanguageProvider.Errors[lang, "AN_ERROR_OCCURRED_TRY_AGAIN"], false);
            }
        }

        public async Task<int> UnreadPMs(int userId)
        {
            var connection = _context.GetDbConnection();
            return await connection.QueryFirstOrDefaultAsync<int>("SELECT count(1) FROM phpbb_privmsgs_to WHERE user_id = @userId AND folder_id >= 0 AND pm_unread = 1", new { userId });
        }

        public async Task<HashSet<AuthenticatedUserExpanded.Permissions>> GetPermissions(int userId)
        {
            var conn = _context.GetDbConnection();
            return new HashSet<AuthenticatedUserExpanded.Permissions>(await conn.QueryAsync<AuthenticatedUserExpanded.Permissions>("CALL get_user_permissions(@userId)", new { userId }));
        }

        public async Task<HashSet<int>> GetFoes(int userId)
        {
            var conn = _context.GetDbConnection();
            return new HashSet<int>(await conn.QueryAsync<int>(
                @"SELECT zebra_id
                    FROM phpbb_zebra
                   WHERE user_id = @user_id 
                     AND foe = 1;",
                new { userId }
            ));
        }

        public async Task<IEnumerable<PhpbbAclRoles>> GetUserRolesLazy()
        {
            if (_userRoles != null)
            {
                return _userRoles;
            }

            var connection = _context.GetDbConnection();

            _userRoles = await connection.QueryAsync<PhpbbAclRoles>("SELECT * FROM phpbb_acl_roles WHERE role_type = 'u_'");

            return _userRoles;
        }

        public async Task<List<KeyValuePair<string, int>>> GetUserMap()
        {
            var connection = _context.GetDbConnection();
            return (
                await connection.QueryAsync("SELECT username, user_id FROM phpbb_users WHERE user_id <> @id AND user_type <> 2 ORDER BY username", new { id = Constants.ANONYMOUS_USER_ID })
            ).Select(u => KeyValuePair.Create((string)u.username, (int)u.user_id)).ToList();
        }

        private async Task<IEnumerable<PhpbbAclRoles>> GetModRolesLazy()
        {
            if (_modRoles != null)
            {
                return _modRoles;
            }

            var connection = _context.GetDbConnection();

            _modRoles = await connection.QueryAsync<PhpbbAclRoles>("SELECT * FROM phpbb_acl_roles WHERE role_type = 'm_'");

            return _modRoles;
        }

        private async Task<IEnumerable<PhpbbAclRoles>> GetAdminRolesLazy()
        {
            if (_adminRoles != null)
            {
                return _adminRoles;
            }

            var connection = _context.GetDbConnection();

            _adminRoles = await connection.QueryAsync<PhpbbAclRoles>("SELECT * FROM phpbb_acl_roles WHERE role_type = 'a_'");

            return _adminRoles;
        }
    }
}
