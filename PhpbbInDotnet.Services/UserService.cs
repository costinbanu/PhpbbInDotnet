using Dapper;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using PhpbbInDotnet.Objects;
using PhpbbInDotnet.Database;
using PhpbbInDotnet.Database.Entities;
using PhpbbInDotnet.Utilities;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Net.Mail;
using System.Security.Claims;
using System.Threading.Tasks;
using PhpbbInDotnet.Languages;

namespace PhpbbInDotnet.Services
{
    public class UserService : MultilingualServiceBase
    {
        private readonly ForumDbContext _context;
        private readonly IConfiguration _config;
        private IEnumerable<PhpbbAclRoles> _adminRoles;
        private IEnumerable<PhpbbAclRoles> _modRoles;
        private IEnumerable<PhpbbAclRoles> _userRoles;

        private static PhpbbUsers _anonymousDbUser;
        private static ClaimsPrincipal _anonymousClaimsPrincipal;

        public UserService(CommonUtils utils, ForumDbContext context, IConfiguration config, LanguageProvider languageProvider, IHttpContextAccessor httpContextAccessor)
            : base(utils, languageProvider, httpContextAccessor)
        {
            _context = context;
            _config = config;
        }

        public async Task<bool> IsUserAdminInForum(AuthenticatedUser user, int forumId)
            => user != null && (
                from up in user.AllPermissions ?? new HashSet<AuthenticatedUser.Permissions>()
                where up.ForumId == forumId || up.ForumId == 0
                join a in await GetAdminRolesLazy()
                on up.AuthRoleId equals a.RoleId
                select up
            ).Any();

        public async Task<bool> IsUserModeratorInForum(AuthenticatedUser user, int forumId)
            => (await IsUserAdminInForum(user, forumId)) || (
                user != null && (
                    from up in user.AllPermissions ?? new HashSet<AuthenticatedUser.Permissions>()
                    where up.ForumId == forumId || up.ForumId == 0
                    join a in await GetModRolesLazy()
                    on up.AuthRoleId equals a.RoleId
                    select up
                ).Any()
            );

        public async Task<int?> GetUserRole(AuthenticatedUser user)
            => (from up in user.AllPermissions ?? new HashSet<AuthenticatedUser.Permissions>()
                join a in await GetUserRolesLazy()
                on up.AuthRoleId equals a.RoleId
                select up.AuthRoleId as int?).FirstOrDefault();

        public async Task<bool> HasPrivateMessagePermissions(int userId)
        {
            var usr = new AuthenticatedUser(await GetAuthenticatedUserById(userId));
            usr.AllPermissions = await GetPermissions(userId);
            return HasPrivateMessagePermissions(usr);
        }

        public bool HasPrivateMessagePermissions(AuthenticatedUser user)
            => !(user?.IsAnonymous ?? true) && !(user?.AllPermissions?.Contains(new AuthenticatedUser.Permissions { ForumId = 0, AuthRoleId = Constants.NO_PM_ROLE }) ?? false);

        public bool HasPrivateMessages(AuthenticatedUser user)
            => user.AllowPM && HasPrivateMessagePermissions(user);

        public async Task<bool> HasPrivateMessages(int userId)
        {
            var usr = new AuthenticatedUser(await GetAuthenticatedUserById(userId));
            usr.AllPermissions = await GetPermissions(userId);
            return HasPrivateMessages(usr);
        }

        private async Task<PhpbbUsers> GetAnonymousDbUser()
        {
            if (_anonymousDbUser != null)
            {
                return _anonymousDbUser;
            }
            var connection = await _context.GetDbConnectionAsync();
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
            var connection = await _context.GetDbConnectionAsync();
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
            identity.AddClaim(new Claim(nameof(AuthenticatedUser.UserId), user.UserId.ToString()));
            identity.AddClaim(new Claim(nameof(AuthenticatedUser.Username), user.Username ?? string.Empty));
            identity.AddClaim(new Claim(nameof(AuthenticatedUser.UsernameClean), user.UsernameClean ?? string.Empty));
            identity.AddClaim(new Claim(nameof(AuthenticatedUser.UserDateFormat), user.UserDateformat ?? string.Empty));
            identity.AddClaim(new Claim(nameof(AuthenticatedUser.UserColor), user.UserColour ?? string.Empty));
            identity.AddClaim(new Claim(nameof(AuthenticatedUser.PostEditTime), ((editTime == 0 || user.UserEditTime == 0) ? 0 : Math.Min(Math.Abs(editTime), Math.Abs(user.UserEditTime))).ToString()));
            identity.AddClaim(new Claim(nameof(AuthenticatedUser.UploadLimit), unchecked((int)groupProperties.group_user_upload_size).ToString()));
            identity.AddClaim(new Claim(nameof(AuthenticatedUser.AllowPM), user.UserAllowPm.ToBool().ToString()));
            identity.AddClaim(new Claim(nameof(AuthenticatedUser.Style), style ?? string.Empty));
            identity.AddClaim(new Claim(nameof(AuthenticatedUser.JumpToUnread), user.JumpToUnread?.ToString() ?? string.Empty));
            identity.AddClaim(new Claim(nameof(AuthenticatedUser.Language), user.UserLang ?? string.Empty));
            identity.AddClaim(new Claim(nameof(AuthenticatedUser.EmailAddress), user.UserEmail ?? string.Empty));
            return new ClaimsPrincipal(identity);
        }

        public AuthenticatedUserBase DbUserToAuthenticatedUserBase(PhpbbUsers dbUser)
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

        public AuthenticatedUser ClaimsPrincipalToAuthenticatedUser(ClaimsPrincipal claimsPrincipal)
        {
            var user = new AuthenticatedUser();
            var found = false;
            foreach (var claim in claimsPrincipal.Claims)
            {
                switch (claim.Type)
                {
                    case nameof(AuthenticatedUser.UserId):
                        user.UserId = int.Parse(claim.Value);
                        found = true;
                        break;
                    case nameof(AuthenticatedUser.Username):
                        user.Username = claim.Value;
                        found = true;
                        break;
                    case nameof(AuthenticatedUser.UsernameClean):
                        user.UsernameClean = claim.Value;
                        found = true;
                        break;
                    case nameof(AuthenticatedUser.UserDateFormat):
                        user.UserDateFormat = claim.Value;
                        found = true;
                        break;
                    case nameof(AuthenticatedUser.UserColor):
                        user.UserColor = claim.Value;
                        found = true;
                        break;
                    case nameof(AuthenticatedUser.PostEditTime):
                        user.PostEditTime = int.Parse(claim.Value);
                        found = true;
                        break;
                    case nameof(AuthenticatedUser.UploadLimit):
                        user.UploadLimit = int.Parse(claim.Value);
                        found = true;
                        break;
                    case nameof(AuthenticatedUser.AllowPM):
                        user.AllowPM = bool.TryParse(claim.Value, out var val) && val;
                        found = true;
                        break;
                    case nameof(AuthenticatedUser.Style):
                        user.Style = claim.Value;
                        found = true;
                        break;
                    case nameof(AuthenticatedUser.JumpToUnread):
                        user.JumpToUnread = bool.TryParse(claim.Value, out val) && val;
                        found = true;
                        break;
                    case nameof(AuthenticatedUser.Language):
                        user.Language = claim.Value;
                        found = true;
                        break;
                    case nameof(AuthenticatedUser.EmailAddress):
                        user.EmailAddress = claim.Value;
                        found = true;
                        break;
                }
            }
            return found ? user : null;
        }

        public async Task<IEnumerable<PhpbbRanks>> GetRankList()
        {
            var connection = await _context.GetDbConnectionAsync();
            return await connection.QueryAsync<PhpbbRanks>("SELECT * FROM phpbb_ranks ORDER BY rank_title");
        }

        public async Task<IEnumerable<PhpbbGroups>> GetGroupList()
        {
            var connection = await _context.GetDbConnectionAsync();
            return await connection.QueryAsync<PhpbbGroups>("SELECT * FROM phpbb_groups ORDER BY group_name");
        }

        public async Task<PhpbbGroups> GetUserGroup(int userId)
        {
            var connection = await _context.GetDbConnectionAsync();
            return await connection.QueryFirstOrDefaultAsync<PhpbbGroups> (
                "SELECT g.* FROM phpbb_groups g " +
                "JOIN phpbb_user_group ug on g.group_id = ug.group_id " +
                "WHERE ug.user_id = @userId", 
                new { userId }
            );
        }

        public async Task<AuthenticatedUserBase> GetAuthenticatedUserById(int userId)
        {
            var connection = await _context.GetDbConnectionAsync();
            var usr = await connection.QuerySingleOrDefaultAsync<PhpbbUsers>("SELECT * FROM phpbb_users WHERE user_id = @userId", new { userId });
            return DbUserToAuthenticatedUserBase(usr);
        }

        public async Task<(string Message, bool? IsSuccess)> SendPrivateMessage(int senderId, string senderName, int receiverId, string subject, string text, PageContext pageContext, HttpContext httpContext)
        {
            var lang = GetLanguage();
            try
            {
                if (!await HasPrivateMessagePermissions(senderId))
                {
                    return (LanguageProvider.Errors[lang, "SENDER_CANT_SEND_PMS"], false);
                }
                
                var receiver = new AuthenticatedUser(await GetAuthenticatedUserById(receiverId));
                receiver.AllPermissions = await GetPermissions(receiverId);
                receiver.Foes = await GetFoes(receiverId);
                if (!HasPrivateMessages(receiver))
                {
                    return (LanguageProvider.Errors[lang, "RECEIVER_CANT_RECEIVE_PMS"], false);
                }
                if (receiver.Foes?.Contains(senderId) ?? false)
                {
                    return (LanguageProvider.Errors[lang, "ON_RECEIVERS_FOE_LIST"], false);
                }

                var connection = await _context.GetDbConnectionAsync();

                await connection.ExecuteAsync(
                    @"INSERT INTO phpbb_privmsgs (author_id, to_address, bcc_address, message_subject, message_text, message_time) VALUES (@senderId, @to, '', @subject, @text, @time); 
                      SELECT LAST_INSERT_ID() INTO @inserted_id;
                      INSERT INTO phpbb_privmsgs_to (author_id, msg_id, user_id, folder_id, pm_unread) VALUES (@senderId, @inserted_id, @receiverId, 0, 1); 
                      INSERT INTO phpbb_privmsgs_to (author_id, msg_id, user_id, folder_id, pm_unread) VALUES (@senderId, @inserted_id, @senderId, -1, 0); ",
                    new { senderId, receiverId, to = $"u_{receiverId}", subject, text, time = DateTime.UtcNow.ToUnixTimestamp() }
                );

                var emailSubject = string.Format(LanguageProvider.Email[receiver.Language, "NEWPM_SUBJECT_FORMAT"], _config.GetValue<string>("ForumName"));
                using var emailMessage = new MailMessage
                {
                    From = new MailAddress(_config.GetValue<string>("AdminEmail"), _config.GetValue<string>("ForumName")),
                    Subject = emailSubject,
                    Body = await Utils.RenderRazorViewToString(
                        "_NewPMEmailPartial",
                        new NewPMEmailDto
                        {
                            SenderName = senderName,
                            Language = receiver.Language
                        },
                        pageContext,
                        httpContext
                    ),
                    IsBodyHtml = true
                };
                emailMessage.To.Add(receiver.EmailAddress);
                await Utils.SendEmail(emailMessage);

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
                var connection = await _context.GetDbConnectionAsync();

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
                var connection = await _context.GetDbConnectionAsync();

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
                var connection = await _context.GetDbConnectionAsync();
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
            var connection = await _context.GetDbConnectionAsync();
            return await connection.ExecuteScalarAsync<int>("SELECT count(1) FROM phpbb_privmsgs_to WHERE user_id = @userId AND folder_id >= 0 AND pm_unread = 1", new { userId });
        }

        public async Task<HashSet<AuthenticatedUser.Permissions>> GetPermissions(int userId)
        {
            var conn = await _context.GetDbConnectionAsync();
            return new HashSet<AuthenticatedUser.Permissions>(await conn.QueryAsync<AuthenticatedUser.Permissions>("CALL get_user_permissions(@userId)", new { userId }));
        }

        public async Task<HashSet<int>> GetFoes(int userId)
        {
            var conn = await _context.GetDbConnectionAsync();
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

            var connection = await _context.GetDbConnectionAsync();
            
            _userRoles = await connection.QueryAsync<PhpbbAclRoles>("SELECT * FROM phpbb_acl_roles WHERE role_type = 'u_'");

            return _userRoles;
        }

        public async Task<List<KeyValuePair<string, int>>> GetUserMap()
        {
            var connection = await _context.GetDbConnectionAsync();
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

            var connection = await _context.GetDbConnectionAsync();

            _modRoles = await connection.QueryAsync<PhpbbAclRoles>("SELECT * FROM phpbb_acl_roles WHERE role_type = 'm_'");           

            return _modRoles;
        }

        private async Task<IEnumerable<PhpbbAclRoles>> GetAdminRolesLazy()
        {
            if (_adminRoles != null)
            {
                return _adminRoles;
            }

            var connection = await _context.GetDbConnectionAsync();

            _adminRoles = await connection.QueryAsync<PhpbbAclRoles>("SELECT * FROM phpbb_acl_roles WHERE role_type = 'a_'");            

            return _adminRoles;
        }
    }
}
