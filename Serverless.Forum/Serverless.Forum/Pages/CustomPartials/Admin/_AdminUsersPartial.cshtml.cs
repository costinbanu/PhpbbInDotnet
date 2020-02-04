﻿using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Serverless.Forum.ForumDb;
using Serverless.Forum.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Serverless.Forum.Pages.CustomPartials.Admin
{
    public class _AdminUsersPartialModel : PageModel
    {
        private readonly IConfiguration _config;
        private readonly Utils _utils;

        public List<PhpbbUsers> SearchResults { get; private set; }
        public string DateFormat { get; set; } = "dddd, dd.MM.yyyy, HH:mm";
        public bool? IsSuccess { get; private set; }
        public string Message { get; private set; }
        public string MessageClass
        {
            get
            {
                switch (IsSuccess)
                {
                    case null:
                        return "message";
                    case true:
                        return "message success";
                    case false:
                    default:
                        return "message fail";
                }
            }
        }

        public _AdminUsersPartialModel(IConfiguration config, Utils utils)
        {
            _config = config;
            _utils = utils;
            SearchResults = new List<PhpbbUsers>();
        }

        public async Task<List<PhpbbUsers>> GetInactiveUsersAsync()
        {
            using (var context = new ForumDbContext(_config))
            {
                return await (
                    from u in context.PhpbbUsers
                    where u.UserInactiveTime > 0
                       && u.UserInactiveReason != UserInactiveReason.NotInactive
                    select u
                ).ToListAsync();
            }
        }

        public async Task ManageUserAsync(AdminUserActions? action, int? userId)
        {
            using (var context = new ForumDbContext(_config))
            {
                if (userId == _utils.AnonymousDbUser.UserId)
                {
                    Message = $"Utilizatorul cu id '{userId}' este utilizatorul anonim și nu poate fi șters.";
                    IsSuccess = false;
                    return;
                }

                var user = await context.PhpbbUsers.FirstOrDefaultAsync(u => u.UserId == userId);
                if (user == null)
                {
                    Message = $"Utilizatorul cu id '{userId}' nu a fost găsit.";
                    IsSuccess = false;
                    return;
                }

                async Task flagUserAsChanged()
                {
                    var key = $"UserMustLogIn_{user.UsernameClean}";
                    await _utils.SetInCacheAsync(key, true);
                }

                async Task deleteUser()
                {
                    context.PhpbbAclUsers.RemoveRange(await context.PhpbbAclUsers.Where(u => u.UserId == userId).ToListAsync());
                    context.PhpbbBanlist.RemoveRange(await context.PhpbbBanlist.Where(u => u.BanUserid == userId).ToListAsync());
                    context.PhpbbBookmarks.RemoveRange(await context.PhpbbBookmarks.Where(u => u.UserId == userId).ToListAsync());
                    context.PhpbbBots.RemoveRange(await context.PhpbbBots.Where(u => u.UserId == userId).ToListAsync());
                    context.PhpbbDrafts.RemoveRange(await context.PhpbbDrafts.Where(u => u.UserId == userId).ToListAsync());
                    context.PhpbbForumsAccess.RemoveRange(await context.PhpbbForumsAccess.Where(u => u.UserId == userId).ToListAsync());
                    context.PhpbbForumsTrack.RemoveRange(await context.PhpbbForumsTrack.Where(u => u.UserId == userId).ToListAsync());
                    context.PhpbbForumsWatch.RemoveRange(await context.PhpbbForumsWatch.Where(u => u.UserId == userId).ToListAsync());
                    context.PhpbbLog.RemoveRange(await context.PhpbbLog.Where(u => u.UserId == userId).ToListAsync());
                    context.PhpbbModeratorCache.RemoveRange(await context.PhpbbModeratorCache.Where(u => u.UserId == userId).ToListAsync());
                    context.PhpbbPollVotes.RemoveRange(await context.PhpbbPollVotes.Where(u => u.VoteUserId == userId).ToListAsync());
                    context.PhpbbPrivmsgsFolder.RemoveRange(await context.PhpbbPrivmsgsFolder.Where(u => u.UserId == userId).ToListAsync());
                    context.PhpbbPrivmsgsRules.RemoveRange(await context.PhpbbPrivmsgsRules.Where(u => u.UserId == userId).ToListAsync());
                    context.PhpbbPrivmsgsTo.RemoveRange(await context.PhpbbPrivmsgsTo.Where(u => u.UserId == userId).ToListAsync());
                    context.PhpbbProfileFieldsData.RemoveRange(await context.PhpbbProfileFieldsData.Where(u => u.UserId == userId).ToListAsync());
                    context.PhpbbReports.RemoveRange(await context.PhpbbReports.Where(u => u.UserId == userId).ToListAsync());
                    context.PhpbbSessions.RemoveRange(await context.PhpbbSessions.Where(u => u.SessionUserId == userId).ToListAsync());
                    context.PhpbbSessionsKeys.RemoveRange(await context.PhpbbSessionsKeys.Where(u => u.UserId == userId).ToListAsync());
                    context.PhpbbTopicsPosted.RemoveRange(await context.PhpbbTopicsPosted.Where(u => u.UserId == userId).ToListAsync());
                    context.PhpbbTopicsTrack.RemoveRange(await context.PhpbbTopicsTrack.Where(u => u.UserId == userId).ToListAsync());
                    context.PhpbbTopicsWatch.RemoveRange(await context.PhpbbTopicsWatch.Where(u => u.UserId == userId).ToListAsync());
                    context.PhpbbUserGroup.RemoveRange(await context.PhpbbUserGroup.Where(u => u.UserId == userId).ToListAsync());
                    context.PhpbbUsers.RemoveRange(await context.PhpbbUsers.Where(u => u.UserId == userId).ToListAsync());
                    context.PhpbbUserTopicPostNumber.RemoveRange(await context.PhpbbUserTopicPostNumber.Where(u => u.UserId == userId).ToListAsync());
                    context.PhpbbWarnings.RemoveRange(await context.PhpbbWarnings.Where(u => u.UserId == userId).ToListAsync());
                    context.PhpbbZebra.RemoveRange(await context.PhpbbZebra.Where(u => u.UserId == userId).ToListAsync());
                    context.PhpbbUsers.Remove(user);
                }

                try
                {
                    switch (action)
                    {
                        case AdminUserActions.Activate:
                            {
                                user.UserInactiveReason = UserInactiveReason.NotInactive;
                                user.UserInactiveTime = 0L;
                                Message = $"Utilizatorul '{user.Username}' a fost activat.";
                                IsSuccess = true;
                                break;
                            }
                        case AdminUserActions.Deactivate:
                            {
                                user.UserInactiveReason = UserInactiveReason.InactivatedByAdmin;
                                user.UserInactiveTime = DateTime.UtcNow.ToUnixTimestamp();
                                await flagUserAsChanged();
                                Message = $"Utilizatorul '{user.Username}' a fost dezactivat.";
                                IsSuccess = true;
                                break;
                            }
                        case AdminUserActions.Delete_KeepMessages:
                            {
                                var posts = await (
                                    from p in context.PhpbbPosts
                                    where p.PosterId == userId
                                    select p
                                ).ToListAsync();

                                posts.ForEach(p =>
                                {
                                    p.PostUsername = user.Username;
                                    p.PosterId = _utils.AnonymousDbUser.UserId;
                                });

                                await flagUserAsChanged();
                                await deleteUser();
                                Message = $"Utilizatorul '{user.Username}' a fost șters iar mesajele păstrate.";
                                IsSuccess = true;
                                break;
                            }
                        case AdminUserActions.Delete_DeleteMessages:
                            {
                                var toDelete = await context.PhpbbPosts.Where(p => p.PosterId == userId).ToListAsync();
                                context.PhpbbPosts.RemoveRange(toDelete);
                                toDelete.ForEach(async p => await _utils.CascadePostDelete(context, p));

                                await flagUserAsChanged();
                                await deleteUser();
                                Message = $"Utilizatorul '{user.Username}' a fost șters cu tot cu mesajele scrise.";
                                IsSuccess = true;
                                break;
                            }
                        default: throw new ArgumentException($"Unknown action '{action}'.", nameof(action));
                    }

                    await context.SaveChangesAsync();
                }
                catch (Exception ex)
                {
                    Message = $"Acțiunea {action} nu a putut fi aplicată utilizatorului '{user.Username}'. Eroare: {ex.Message}";
                    IsSuccess = false;
                    return;
                }
            }
        }

        public async Task UserSearchAsync(string username, string email, int? userid)
        {
            using (var context = new ForumDbContext(_config))
            {
                SearchResults = await (
                    from u in context.PhpbbUsers
                    where (string.IsNullOrWhiteSpace(username) || u.UsernameClean.Contains(_utils.CleanString(username), StringComparison.InvariantCultureIgnoreCase))
                       && (string.IsNullOrWhiteSpace(email) || u.UserEmail == email)
                       && ((userid ?? 0) == 0 || u.UserId == userid)
                    select u
                ).ToListAsync();
            }
        }
    }
}