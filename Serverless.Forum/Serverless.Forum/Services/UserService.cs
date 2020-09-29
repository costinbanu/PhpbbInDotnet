﻿using Dapper;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Serverless.Forum.Contracts;
using Serverless.Forum.ForumDb;
using Serverless.Forum.ForumDb.Entities;
using Serverless.Forum.Pages.CustomPartials.Email;
using Serverless.Forum.Utilities;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Net.Mail;
using System.Security.Claims;
using System.Threading.Tasks;

namespace Serverless.Forum.Services
{
    public class UserService
    {
        private readonly Utils _utils;
        private readonly ForumDbContext _context;
        private readonly IConfiguration _config;
        private IEnumerable<PhpbbAclRoles> _adminRoles;
        private IEnumerable<PhpbbAclRoles> _modRoles;
        private IEnumerable<PhpbbAclRoles> _userRoles;

        private static PhpbbUsers _anonymousDbUser;
        private static ClaimsPrincipal _anonymousClaimsPrincipal;

        public UserService(Utils utils, ForumDbContext context, IConfiguration config)
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

        public bool HasPrivateMessages(LoggedUser user)
            => !(user?.IsAnonymous ?? true) && !(user?.AllPermissions?.Contains(new LoggedUser.Permissions { ForumId = 0, AuthRoleId = Constants.NO_PM_ROLE }) ?? false);

        public async Task<bool> HasPrivateMessages(int userId)
            => HasPrivateMessages(await GetLoggedUserById(userId));

        async Task<PhpbbUsers> GetAnonymousDbUserAsync()
        {
            if (_anonymousDbUser != null)
            {
                return _anonymousDbUser;
            }
            using (var connection = _context.Database.GetDbConnection())
            {
                await connection.OpenIfNeeded();
                _anonymousDbUser = await connection.QuerySingleOrDefaultAsync<PhpbbUsers>("SELECT * FROM phpbb_users WHERE user_id = @userId", new { userId = Constants.ANONYMOUS_USER_ID });
            }
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

            using var connection = _context.Database.GetDbConnection();
            await connection.OpenIfNeeded();
            var editTime = await connection.QuerySingleOrDefaultAsync<int>(
                @"SELECT group_edit_time g
                   FROM phpbb_groups g
                   JOIN phpbb_user_group ug ON g.group_id = ug.group_id
                  WHERE ug.user_id = @UserId
                  LIMIT 1",
                new { user.UserId }
            );
            using var multi = await connection.QueryMultipleAsync("CALL `forum`.`get_user_details`(@UserId);", new { user.UserId });
            var intermediary = new LoggedUser
            {
                UserId = user.UserId,
                Username = user.Username,
                UsernameClean = user.UsernameClean,
                AllPermissions = new HashSet<LoggedUser.Permissions>(await multi.ReadAsync<LoggedUser.Permissions>()),
                TopicPostsPerPage = (await multi.ReadAsync()).ToDictionary(key => checked((int)key.topic_id), value => checked((int)value.post_no)),
                UserDateFormat = user.UserDateformat,
                UserColor = user.UserColour,
                PostEditTime = (editTime == 0 || user.UserEditTime == 0) ? 0 : Math.Min(Math.Abs(editTime), Math.Abs(user.UserEditTime))
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
            using var connection = _context.Database.GetDbConnection();
            await connection.OpenIfNeeded();
            return await connection.QueryAsync<PhpbbRanks>("SELECT * FROM phpbb_ranks ORDER BY rank_title");
        }

        public async Task<IEnumerable<PhpbbGroups>> GetGroupListAsync()
        {
            using var connection = _context.Database.GetDbConnection();
            await connection.OpenIfNeeded();
            return await connection.QueryAsync<PhpbbGroups>("SELECT * FROM phpbb_groups ORDER BY group_name");
        }

        public async Task<int?> GetUserGroupAsync(int userId)
        {
            using var connection = _context.Database.GetDbConnection();
            await connection.OpenIfNeeded();
            var usr = await connection.QuerySingleOrDefaultAsync<PhpbbUserGroup>("SELECT * FROM phpbb_users WHERE user_id = @userId", new { userId });
            return usr?.GroupId;
        }

        public async Task<LoggedUser> GetLoggedUserById(int userId)
        {
            using var connection = _context.Database.GetDbConnection();
            await connection.OpenIfNeeded();
            var usr = await connection.QuerySingleOrDefaultAsync<PhpbbUsers>("SELECT * FROM phpbb_users WHERE user_id = @userId", new { userId });
            return await DbUserToLoggedUserAsync(usr);
        }

        public async Task<(string Message, bool? IsSuccess)> SendPrivateMessage(int senderId, string senderName, int receiverId, string subject, string text, PageContext pageContext, HttpContext httpContext)
        {
            try
            {
                if (!await HasPrivateMessages(senderId))
                {
                    return ("Expeditorul nu are dreptul să trimită mesaje private.", false);
                }
                if (!await HasPrivateMessages(receiverId))
                {
                    return ("Destinatarul nu are dreptul să primească mesaje private.", false);
                }

                var msgResult = _context.PhpbbPrivmsgs.Add(new PhpbbPrivmsgs
                {
                    AuthorId = senderId,
                    ToAddress = $"u_{receiverId}",
                    MessageSubject = subject,
                    MessageText = text,
                    MessageTime = DateTime.UtcNow.ToUnixTimestamp()
                });
                msgResult.Entity.MsgId = 0;
                await _context.SaveChangesAsync();
                var msg = msgResult.Entity;

                var readResult = _context.PhpbbPrivmsgsTo.Add(new PhpbbPrivmsgsTo
                {
                    AuthorId = senderId,
                    MsgId = msg.MsgId,
                    UserId = receiverId
                });
                readResult.Entity.Id = 0;
                await _context.SaveChangesAsync();

                using var emailMessage = new MailMessage
                {
                    From = new MailAddress($"admin@metrouusor.com", _config.GetValue<string>("ForumName")),
                    Subject = $"Ai primit un mesaj privat nou pe {_config.GetValue<string>("ForumName")}",
                    Body = await _utils.RenderRazorViewToString(
                        "_NewPMEmailPartial",
                        new _NewPMEmailPartialModel
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
                var to = await _context.PhpbbPrivmsgsTo.AsNoTracking().FirstOrDefaultAsync(x => x.MsgId == messageId && x.AuthorId != x.UserId);
                if (to.PmUnread != 1)
                {
                    return ("Mesajul a fost deja citit și nu mai poate fi actualizat.", false);
                }
                var pm = await _context.PhpbbPrivmsgs.FirstOrDefaultAsync(x => x.MsgId == messageId);
                pm.MessageSubject = subject;
                pm.MessageText = text;
                await _context.SaveChangesAsync();
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
                var msg = await _context.PhpbbPrivmsgs.FirstOrDefaultAsync(p => p.MsgId == messageId);
                if (msg == null)
                {
                    return ("Mesajul nu există.", false);
                }
                var msgToEntries = await _context.PhpbbPrivmsgsTo.Where(t => t.MsgId == messageId).ToListAsync();
                var to = msgToEntries.FirstOrDefault(x => x.AuthorId != x.UserId);
                if (to.PmUnread != 1)
                {
                    return ("Mesajul a fost deja citit și nu mai poate fi șters.", false);
                }
                _context.PhpbbPrivmsgs.Remove(msg);
                _context.PhpbbPrivmsgsTo.RemoveRange(msgToEntries);
                await _context.SaveChangesAsync();
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
            using var connection = _context.Database.GetDbConnection();
            await connection.OpenIfNeeded();
            return await connection.ExecuteScalarAsync<int>("SELECT count(1) FROM phpbb_privmsgs_to WHERE user_id = @userId AND author_id <> user_id AND pm_unread = 1", new { userId });
        }

        public async Task<IEnumerable<PhpbbAclRoles>> GetUserRolesLazy()
        {
            if (_userRoles != null)
            {
                return _userRoles;
            }

            using (var connection = _context.Database.GetDbConnection())
            {
                await connection.OpenIfNeeded();
                _userRoles = await connection.QueryAsync<PhpbbAclRoles>("SELECT * FROM phpbb_acl_roles WHERE role_type = 'u_'");
            }

            return _userRoles;
        }

        private async Task<IEnumerable<PhpbbAclRoles>> GetModRolesLazy()
        {
            if (_modRoles != null)
            {
                return _modRoles;
            }

            using (var connection = _context.Database.GetDbConnection())
            {
                await connection.OpenIfNeeded();
                _modRoles = await connection.QueryAsync<PhpbbAclRoles>("SELECT * FROM phpbb_acl_roles WHERE role_type = 'm_'");
            }

            return _modRoles;
        }

        private async Task<IEnumerable<PhpbbAclRoles>> GetAdminRolesLazy()
        {
            if (_adminRoles != null)
            {
                return _adminRoles;
            }

            using (var connection = _context.Database.GetDbConnection())
            {
                await connection.OpenIfNeeded();
                _adminRoles = await connection.QueryAsync<PhpbbAclRoles>("SELECT * FROM phpbb_acl_roles WHERE role_type = 'a_'");
            }

            return _adminRoles;
        }
    }
}
