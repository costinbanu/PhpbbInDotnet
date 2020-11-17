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

namespace PhpbbInDotnet.Services
{
    public class UserService
    {
        private readonly CommonUtils _utils;
        private readonly ForumDbContext _context;
        private readonly IConfiguration _config;
        private IEnumerable<PhpbbAclRoles> _adminRoles;
        private IEnumerable<PhpbbAclRoles> _modRoles;
        private IEnumerable<PhpbbAclRoles> _userRoles;

        private static PhpbbUsers _anonymousDbUser;
        private static ClaimsPrincipal _anonymousClaimsPrincipal;

        public UserService(CommonUtils utils, ForumDbContext context, IConfiguration config)
        {
            _utils = utils;
            _context = context;
            _config = config;
        }

        public async Task<bool> IsUserAdminInForum(LoggedUser user, int forumId)
            => user != null && (
                from up in user.AllPermissions ?? new HashSet<LoggedUser.Permissions>()
                where up.ForumId == forumId || up.ForumId == 0
                join a in await GetAdminRolesLazy()
                on up.AuthRoleId equals a.RoleId
                select up
            ).Any();

        public async Task<bool> IsUserModeratorInForum(LoggedUser user, int forumId)
            => (await IsUserAdminInForum(user, forumId)) || (
                user != null && (
                    from up in user.AllPermissions ?? new HashSet<LoggedUser.Permissions>()
                    where up.ForumId == forumId || up.ForumId == 0
                    join a in await GetModRolesLazy()
                    on up.AuthRoleId equals a.RoleId
                    select up
                ).Any()
            );

        public async Task<int?> GetUserRole(LoggedUser user)
            => (from up in user.AllPermissions ?? new HashSet<LoggedUser.Permissions>()
                join a in await GetUserRolesLazy()
                on up.AuthRoleId equals a.RoleId
                select up.AuthRoleId as int?).FirstOrDefault();

        public async Task<bool> HasPrivateMessagePermissions(int userId)
            => HasPrivateMessagePermissions(await GetLoggedUserById(userId));

        public bool HasPrivateMessagePermissions(LoggedUser user)
            => !(user?.IsAnonymous ?? true) && !(user?.AllPermissions?.Contains(new LoggedUser.Permissions { ForumId = 0, AuthRoleId = Constants.NO_PM_ROLE }) ?? false);

        public bool HasPrivateMessages(LoggedUser user)
            => user.AllowPM && HasPrivateMessagePermissions(user);

        public async Task<bool> HasPrivateMessages(int userId)
            => HasPrivateMessages(await GetLoggedUserById(userId));

        async Task<PhpbbUsers> GetAnonymousDbUserAsync()
        {
            if (_anonymousDbUser != null)
            {
                return _anonymousDbUser;
            }
            var connection = await _context.GetDbConnectionAndOpenAsync();

            _anonymousDbUser = await connection.QuerySingleOrDefaultAsync<PhpbbUsers>("SELECT * FROM phpbb_users WHERE user_id = @userId", new { userId = Constants.ANONYMOUS_USER_ID });
            
            return _anonymousDbUser;
        }

        public async Task<ClaimsPrincipal> GetAnonymousClaimsPrincipalAsync()
        {
            if (_anonymousClaimsPrincipal != null)
            {
                return _anonymousClaimsPrincipal;
            }

            _anonymousClaimsPrincipal = await DbUserToClaimsPrincipalAsync(await GetAnonymousDbUserAsync());
            return _anonymousClaimsPrincipal;
        }

        public async Task<ClaimsPrincipal> DbUserToClaimsPrincipalAsync(PhpbbUsers user)
        {
            var connection = await _context.GetDbConnectionAndOpenAsync();
            using var multi = await connection.QueryMultipleAsync(
                @"CALL `forum`.`get_user_details`(@UserId);

                SELECT group_edit_time g
                  FROM phpbb_groups g
                  JOIN phpbb_user_group ug ON g.group_id = ug.group_id
                 WHERE ug.user_id = @UserId
                 LIMIT 1;

                SELECT style_name 
                  FROM phpbb_styles 
                 WHERE style_id = @UserStyle", 
                new { user.UserId, user.UserStyle }
            );

            var permissions = new HashSet<LoggedUser.Permissions>(await multi.ReadAsync<LoggedUser.Permissions>());
            var tpp = (await multi.ReadAsync()).ToDictionary(x => checked((int)x.topic_id), y => checked((int)y.post_no));
            var foes = new HashSet<int>((await multi.ReadAsync<uint>()).Select(x => unchecked((int)x)));
            var editTime = unchecked((int)await multi.ReadSingleOrDefaultAsync<uint>());
            var style = await multi.ReadFirstOrDefaultAsync<string>();

            var intermediary = new LoggedUser
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
                Style = style
            };

            var identity = new ClaimsIdentity(CookieAuthenticationDefaults.AuthenticationScheme);
            identity.AddClaim(new Claim(ClaimTypes.UserData, Convert.ToBase64String(await _utils.CompressObject(intermediary))));
            return new ClaimsPrincipal(identity);
        }

        public async Task<LoggedUser> ClaimsPrincipalToLoggedUserAsync(ClaimsPrincipal principal)
            => await _utils.DecompressObject<LoggedUser>(Convert.FromBase64String(principal?.Claims?.FirstOrDefault()?.Value ?? string.Empty));

        public async Task<LoggedUser> DbUserToLoggedUserAsync(PhpbbUsers dbUser)
            => await ClaimsPrincipalToLoggedUserAsync(await DbUserToClaimsPrincipalAsync(dbUser));

        public async Task<IEnumerable<PhpbbRanks>> GetRankListAsync()
        {
            var connection = await _context.GetDbConnectionAndOpenAsync();
            return await connection.QueryAsync<PhpbbRanks>("SELECT * FROM phpbb_ranks ORDER BY rank_title");
        }

        public async Task<IEnumerable<PhpbbGroups>> GetGroupListAsync()
        {
            var connection = await _context.GetDbConnectionAndOpenAsync();
            return await connection.QueryAsync<PhpbbGroups>("SELECT * FROM phpbb_groups ORDER BY group_name");
        }

        public async Task<PhpbbGroups> GetUserGroupAsync(int userId)
        {
            var connection = await _context.GetDbConnectionAndOpenAsync();
            return await connection.QueryFirstOrDefaultAsync<PhpbbGroups> (
                "SELECT g.* FROM phpbb_groups g " +
                "JOIN phpbb_user_group ug on g.group_id = ug.group_id " +
                "WHERE ug.user_id = @userId", 
                new { userId }
            );
        }

        public async Task<LoggedUser> GetLoggedUserById(int userId)
        {
            var connection = await _context.GetDbConnectionAndOpenAsync();
            var usr = await connection.QuerySingleOrDefaultAsync<PhpbbUsers>("SELECT * FROM phpbb_users WHERE user_id = @userId", new { userId });
            return await DbUserToLoggedUserAsync(usr);
        }

        public async Task<(string Message, bool? IsSuccess)> SendPrivateMessage(int senderId, string senderName, int receiverId, string subject, string text, PageContext pageContext, HttpContext httpContext)
        {
            try
            {
                if (!await HasPrivateMessagePermissions(senderId))
                {
                    return ("Expeditorul nu are dreptul să trimită mesaje private.", false);
                }
                
                var receiver = await GetLoggedUserById(receiverId);
                if (!HasPrivateMessages(receiver))
                {
                    return ("Destinatarul nu poate primi mesaje private.", false);
                }
                if (receiver.Foes?.Contains(senderId) ?? false)
                {
                    return ("Destinatarul te-a adăugat pe lista sa de persoane neagreate, drept urmare nu îi poți trimite mesaje private.", false);
                }

                var connection = await _context.GetDbConnectionAndOpenAsync();

                await connection.ExecuteAsync(
                    @"INSERT INTO phpbb_privmsgs (author_id, to_address, bcc_address, message_subject, message_text, message_time) VALUES (@senderId, @to, '', @subject, @text, @time); 
                      SELECT LAST_INSERT_ID() INTO @inserted_id;
                      INSERT INTO phpbb_privmsgs_to (author_id, msg_id, user_id, folder_id, pm_unread) VALUES (@senderId, @inserted_id, @receiverId, 0, 1); 
                      INSERT INTO phpbb_privmsgs_to (author_id, msg_id, user_id, folder_id, pm_unread) VALUES (@senderId, @inserted_id, @senderId, -1, 0); ",
                    new { senderId, receiverId, to = $"u_{receiverId}", subject, text, time = DateTime.UtcNow.ToUnixTimestamp() }
                );

                using var emailMessage = new MailMessage
                {
                    From = new MailAddress($"admin@metrouusor.com", _config.GetValue<string>("ForumName")),
                    Subject = $"Ai primit un mesaj privat nou pe {_config.GetValue<string>("ForumName")}",
                    Body = await _utils.RenderRazorViewToString(
                        "_NewPMEmailPartial",
                        new NewPMEmailDto
                        {
                            SenderName = senderName
                        },
                        pageContext,
                        httpContext
                    ),
                    IsBodyHtml = true
                };
                emailMessage.To.Add((await _context.PhpbbUsers.AsNoTracking().FirstOrDefaultAsync(u => u.UserId == receiverId)).UserEmail);
                await _utils.SendEmail(emailMessage);

                return ("OK", true);
            }
            catch (Exception ex)
            {
                _utils.HandleError(ex);
                return ("A intervenit o eroare, încearcă mai târziu.", false);
            }
        }

        public async Task<(string Message, bool? IsSuccess)> EditPrivateMessage(int messageId, string subject, string text)
        {
            try
            {
                var connection = await _context.GetDbConnectionAndOpenAsync();

                var rows = await connection.ExecuteAsync(
                    "UPDATE phpbb_privmsgs SET message_subject = @subject, message_text = @text " +
                    "WHERE msg_id = @messageId AND EXISTS(SELECT 1 FROM phpbb_privmsgs_to WHERE msg_id = @messageId AND pm_unread = 1 AND user_id <> author_id)",
                    new { subject, text, messageId }
                );

                if (rows == 0)
                {
                    return ("Mesajul a fost deja citit și nu mai poate fi actualizat.", false);
                }

                return ("OK", true);
            }
            catch (Exception ex)
            {
                _utils.HandleError(ex);
                return ("A intervenit o eroare, încearcă mai târziu.", false);
            }
        }

        public async Task<(string Message, bool? IsSuccess)> DeletePrivateMessage(int messageId)
        {
            try
            {
                var connection = await _context.GetDbConnectionAndOpenAsync();

                var rows = await connection.ExecuteAsync(
                    "DELETE m FROM phpbb_privmsgs m JOIN phpbb_privmsgs_to tt ON m.msg_id = tt.msg_id AND tt.pm_unread = 1 WHERE m.msg_id = @messageId; " +
                    "DELETE t FROM phpbb_privmsgs_to t JOIN phpbb_privmsgs_to tt ON t.msg_id = tt.msg_id AND tt.pm_unread = 1 WHERE t.msg_id = @messageId;",
                    new { messageId }
                );

                if (rows == 0)
                {
                    return ("Mesajul nu există sau a fost deja citit. Drept urmare nu mai poate fi șters.", false);
                }

                return ("OK", true);
            }
            catch (Exception ex)
            {
                _utils.HandleError(ex);
                return ("A intervenit o eroare, încearcă mai târziu.", false);
            }
        }

        public async Task<(string Message, bool? IsSuccess)> HidePrivateMessages(int userId, params int[] messageIds)
        {
            try
            {
                using var connection = await _context.GetDbConnectionAndOpenAsync();
                var rows = await connection.ExecuteAsync(
                    @"UPDATE phpbb_privmsgs_to
                         SET folder_id = -10
                       WHERE msg_id IN @messageIds AND user_id = @userId",
                    new { messageIds, userId }
                );
                if (rows < messageIds.Length)
                {
                    return ($"{rows} mesaje au fost șterse cu succes, însă {messageIds.Length - rows} nu au fost găsite.", false);
                }
                return ("OK", true);
            }
            catch (Exception ex)
            {
                _utils.HandleError(ex);
                return ("A intervenit o eroare, încearcă mai târziu.", false);
            }
        }

        public async Task<int> UnreadPMs(int userId)
        {
            var connection = await _context.GetDbConnectionAndOpenAsync();
            return await connection.ExecuteScalarAsync<int>("SELECT count(1) FROM phpbb_privmsgs_to WHERE user_id = @userId AND folder_id >= 0 AND pm_unread = 1", new { userId });
        }

        public async Task<IEnumerable<PhpbbAclRoles>> GetUserRolesLazy()
        {
            if (_userRoles != null)
            {
                return _userRoles;
            }

            var connection = await _context.GetDbConnectionAndOpenAsync();
            
            _userRoles = await connection.QueryAsync<PhpbbAclRoles>("SELECT * FROM phpbb_acl_roles WHERE role_type = 'u_'");

            return _userRoles;
        }

        private async Task<IEnumerable<PhpbbAclRoles>> GetModRolesLazy()
        {
            if (_modRoles != null)
            {
                return _modRoles;
            }

            var connection = await _context.GetDbConnectionAndOpenAsync();

            _modRoles = await connection.QueryAsync<PhpbbAclRoles>("SELECT * FROM phpbb_acl_roles WHERE role_type = 'm_'");           

            return _modRoles;
        }

        private async Task<IEnumerable<PhpbbAclRoles>> GetAdminRolesLazy()
        {
            if (_adminRoles != null)
            {
                return _adminRoles;
            }

            var connection = await _context.GetDbConnectionAndOpenAsync();

            _adminRoles = await connection.QueryAsync<PhpbbAclRoles>("SELECT * FROM phpbb_acl_roles WHERE role_type = 'a_'");            

            return _adminRoles;
        }
    }
}
