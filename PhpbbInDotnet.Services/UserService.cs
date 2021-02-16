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
            => HasPrivateMessagePermissions(await GetAuthenticatedUserById(userId));

        public bool HasPrivateMessagePermissions(AuthenticatedUser user)
            => !(user?.IsAnonymous ?? true) && !(user?.AllPermissions?.Contains(new AuthenticatedUser.Permissions { ForumId = 0, AuthRoleId = Constants.NO_PM_ROLE }) ?? false);

        public bool HasPrivateMessages(AuthenticatedUser user)
            => user.AllowPM && HasPrivateMessagePermissions(user);

        public async Task<bool> HasPrivateMessages(int userId)
            => HasPrivateMessages(await GetAuthenticatedUserById(userId));

        private async Task<PhpbbUsers> GetAnonymousDbUser()
        {
            if (_anonymousDbUser != null)
            {
                return _anonymousDbUser;
            }
            var connection = _context.Database.GetDbConnection();

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
            var connection = _context.Database.GetDbConnection();
            using var multi = await connection.QueryMultipleAsync(
                @"CALL `forum`.`get_user_details`(@UserId);

                SELECT g.group_edit_time, g.group_user_upload_size
                  FROM phpbb_groups g
                  JOIN phpbb_users u ON g.group_id = u.group_id
                 WHERE u.user_id = @UserId
                 LIMIT 1;

                SELECT style_name 
                  FROM phpbb_styles 
                 WHERE style_id = @UserStyle", 
                new { user.UserId, user.UserStyle }
            );

            var permissions = new HashSet<AuthenticatedUser.Permissions>(await multi.ReadAsync<AuthenticatedUser.Permissions>());
            var tpp = (await multi.ReadAsync()).ToDictionary(x => checked((int)x.topic_id), y => checked((int)y.post_no));
            var foes = new HashSet<int>((await multi.ReadAsync<uint>()).Select(x => unchecked((int)x)));
            var groupProperties = await multi.ReadFirstOrDefaultAsync();
            var editTime = unchecked((int)groupProperties.group_edit_time);
            var style = await multi.ReadFirstOrDefaultAsync<string>();

            var intermediary = new AuthenticatedUser
            {
                UserId = user.UserId,
                Username = user.Username,
                UsernameClean = user.UsernameClean,
                AllPermissions = permissions,
                TopicPostsPerPage = tpp,
                Foes = foes,
                UserDateFormat = user.UserDateformat,
                UserColor = user.UserColour,
                PostEditTime = (editTime == 0 || user.UserEditTime == 0) ? 0 : Math.Min(Math.Abs(editTime), Math.Abs(user.UserEditTime)),
                AllowPM = user.UserAllowPm.ToBool(),
                Style = style,
                JumpToUnread = user.JumpToUnread,
                UploadLimit = unchecked((int)groupProperties.group_user_upload_size),
                Language = user.UserLang,
                EmailAddress = user.UserEmail
            };

            var identity = new ClaimsIdentity(CookieAuthenticationDefaults.AuthenticationScheme);
            identity.AddClaim(new Claim(ClaimTypes.UserData, Convert.ToBase64String(await Utils.CompressObject(intermediary))));
            return new ClaimsPrincipal(identity);
        }

        public async new Task<AuthenticatedUser> ClaimsPrincipalToAuthenticatedUser(ClaimsPrincipal principal)
            => await base.ClaimsPrincipalToAuthenticatedUser(principal);

        public async Task<AuthenticatedUser> DbUserToAuthenticatedUser(PhpbbUsers dbUser)
            => await ClaimsPrincipalToAuthenticatedUser(await DbUserToClaimsPrincipal(dbUser));

        public async Task<IEnumerable<PhpbbRanks>> GetRankList()
        {
            var connection = _context.Database.GetDbConnection();
            return await connection.QueryAsync<PhpbbRanks>("SELECT * FROM phpbb_ranks ORDER BY rank_title");
        }

        public async Task<IEnumerable<PhpbbGroups>> GetGroupList()
        {
            var connection = _context.Database.GetDbConnection();
            return await connection.QueryAsync<PhpbbGroups>("SELECT * FROM phpbb_groups ORDER BY group_name");
        }

        public async Task<PhpbbGroups> GetUserGroup(int userId)
        {
            var connection = _context.Database.GetDbConnection();
            return await connection.QueryFirstOrDefaultAsync<PhpbbGroups> (
                "SELECT g.* FROM phpbb_groups g " +
                "JOIN phpbb_user_group ug on g.group_id = ug.group_id " +
                "WHERE ug.user_id = @userId", 
                new { userId }
            );
        }

        public async Task<AuthenticatedUser> GetAuthenticatedUserById(int userId)
        {
            var connection = _context.Database.GetDbConnection();
            var usr = await connection.QuerySingleOrDefaultAsync<PhpbbUsers>("SELECT * FROM phpbb_users WHERE user_id = @userId", new { userId });
            return await DbUserToAuthenticatedUser(usr);
        }

        public async Task<(string Message, bool? IsSuccess)> SendPrivateMessage(int senderId, string senderName, int receiverId, string subject, string text, PageContext pageContext, HttpContext httpContext)
        {
            var lang = await GetLanguage();
            try
            {
                if (!await HasPrivateMessagePermissions(senderId))
                {
                    return (LanguageProvider.Errors[lang, "SENDER_CANT_SEND_PMS"], false);
                }
                
                var receiver = await GetAuthenticatedUserById(receiverId);
                if (!HasPrivateMessages(receiver))
                {
                    return (LanguageProvider.Errors[lang, "RECEIVER_CANT_RECEIVE_PMS"], false);
                }
                if (receiver.Foes?.Contains(senderId) ?? false)
                {
                    return (LanguageProvider.Errors[lang, "ON_RECEIVERS_FOE_LIST"], false);
                }

                var connection = _context.Database.GetDbConnection();

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
                    From = new MailAddress($"admin@metrouusor.com", _config.GetValue<string>("ForumName")),
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
            var lang = await GetLanguage();
            try
            {
                var connection = _context.Database.GetDbConnection();

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
            var lang = await GetLanguage();
            try
            {
                var connection = _context.Database.GetDbConnection();

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
            var lang = await GetLanguage();
            try
            {
                using var connection = _context.Database.GetDbConnection();
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
            var connection = _context.Database.GetDbConnection();
            return await connection.ExecuteScalarAsync<int>("SELECT count(1) FROM phpbb_privmsgs_to WHERE user_id = @userId AND folder_id >= 0 AND pm_unread = 1", new { userId });
        }

        public async Task<IEnumerable<PhpbbAclRoles>> GetUserRolesLazy()
        {
            if (_userRoles != null)
            {
                return _userRoles;
            }

            var connection = _context.Database.GetDbConnection();
            
            _userRoles = await connection.QueryAsync<PhpbbAclRoles>("SELECT * FROM phpbb_acl_roles WHERE role_type = 'u_'");

            return _userRoles;
        }

        private async Task<IEnumerable<PhpbbAclRoles>> GetModRolesLazy()
        {
            if (_modRoles != null)
            {
                return _modRoles;
            }

            var connection = _context.Database.GetDbConnection();

            _modRoles = await connection.QueryAsync<PhpbbAclRoles>("SELECT * FROM phpbb_acl_roles WHERE role_type = 'm_'");           

            return _modRoles;
        }

        private async Task<IEnumerable<PhpbbAclRoles>> GetAdminRolesLazy()
        {
            if (_adminRoles != null)
            {
                return _adminRoles;
            }

            var connection = _context.Database.GetDbConnection();

            _adminRoles = await connection.QueryAsync<PhpbbAclRoles>("SELECT * FROM phpbb_acl_roles WHERE role_type = 'a_'");            

            return _adminRoles;
        }
    }
}
