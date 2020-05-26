using Microsoft.EntityFrameworkCore;
using Serverless.Forum.ForumDb;
using Serverless.Forum.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Serverless.Forum.Services
{
    public class AdminUserService
    {
        private readonly ForumDbContext _context;
        private readonly Utils _utils;
        private readonly PostService _postService;
        private readonly UserService _userService;
        private readonly CacheService _cacheService;

        public AdminUserService(ForumDbContext context, Utils utils, PostService postService, UserService userService, CacheService cacheService)
        {
            _context = context;
            _utils = utils;
            _postService = postService;
            _userService = userService;
            _cacheService = cacheService;
        }

        public async Task<List<PhpbbUsers>> GetInactiveUsersAsync()
            => await (
                from u in _context.PhpbbUsers.AsNoTracking()
                where u.UserInactiveTime > 0
                    && u.UserInactiveReason != UserInactiveReason.NotInactive
                select u
            ).ToListAsync();

        public async Task<(string Message, bool? IsSuccess)> ManageUserAsync(AdminUserActions? action, int? userId)
        {

            if (userId == (await _userService.GetAnonymousDbUserAsync()).UserId)
            {
                return ($"Utilizatorul cu id '{userId}' este utilizatorul anonim și nu poate fi șters.", false);
            }

            var user = await _context.PhpbbUsers.FirstOrDefaultAsync(u => u.UserId == userId);
            if (user == null)
            {
                return ($"Utilizatorul cu id '{userId}' nu a fost găsit.", false);
            }

            async Task flagUserAsChanged()
            {
                var key = $"UserMustLogIn_{user.UsernameClean}";
                await _cacheService.SetInCache(key, true);
            }

            async Task deleteUser()
            {
                _context.PhpbbAclUsers.RemoveRange(await _context.PhpbbAclUsers.Where(u => u.UserId == userId).ToListAsync());
                _context.PhpbbBanlist.RemoveRange(await _context.PhpbbBanlist.Where(u => u.BanUserid == userId).ToListAsync());
                _context.PhpbbBookmarks.RemoveRange(await _context.PhpbbBookmarks.Where(u => u.UserId == userId).ToListAsync());
                _context.PhpbbBots.RemoveRange(await _context.PhpbbBots.Where(u => u.UserId == userId).ToListAsync());
                _context.PhpbbDrafts.RemoveRange(await _context.PhpbbDrafts.Where(u => u.UserId == userId).ToListAsync());
                _context.PhpbbForumsAccess.RemoveRange(await _context.PhpbbForumsAccess.Where(u => u.UserId == userId).ToListAsync());
                _context.PhpbbForumsTrack.RemoveRange(await _context.PhpbbForumsTrack.Where(u => u.UserId == userId).ToListAsync());
                _context.PhpbbForumsWatch.RemoveRange(await _context.PhpbbForumsWatch.Where(u => u.UserId == userId).ToListAsync());
                _context.PhpbbLog.RemoveRange(await _context.PhpbbLog.Where(u => u.UserId == userId).ToListAsync());
                _context.PhpbbModeratorCache.RemoveRange(await _context.PhpbbModeratorCache.Where(u => u.UserId == userId).ToListAsync());
                _context.PhpbbPollVotes.RemoveRange(await _context.PhpbbPollVotes.Where(u => u.VoteUserId == userId).ToListAsync());
                _context.PhpbbPrivmsgsFolder.RemoveRange(await _context.PhpbbPrivmsgsFolder.Where(u => u.UserId == userId).ToListAsync());
                _context.PhpbbPrivmsgsRules.RemoveRange(await _context.PhpbbPrivmsgsRules.Where(u => u.UserId == userId).ToListAsync());
                _context.PhpbbPrivmsgsTo.RemoveRange(await _context.PhpbbPrivmsgsTo.Where(u => u.UserId == userId).ToListAsync());
                _context.PhpbbProfileFieldsData.RemoveRange(await _context.PhpbbProfileFieldsData.Where(u => u.UserId == userId).ToListAsync());
                _context.PhpbbReports.RemoveRange(await _context.PhpbbReports.Where(u => u.UserId == userId).ToListAsync());
                _context.PhpbbSessions.RemoveRange(await _context.PhpbbSessions.Where(u => u.SessionUserId == userId).ToListAsync());
                _context.PhpbbSessionsKeys.RemoveRange(await _context.PhpbbSessionsKeys.Where(u => u.UserId == userId).ToListAsync());
                _context.PhpbbTopicsPosted.RemoveRange(await _context.PhpbbTopicsPosted.Where(u => u.UserId == userId).ToListAsync());
                _context.PhpbbTopicsTrack.RemoveRange(await _context.PhpbbTopicsTrack.Where(u => u.UserId == userId).ToListAsync());
                _context.PhpbbTopicsWatch.RemoveRange(await _context.PhpbbTopicsWatch.Where(u => u.UserId == userId).ToListAsync());
                _context.PhpbbUserGroup.RemoveRange(await _context.PhpbbUserGroup.Where(u => u.UserId == userId).ToListAsync());
                _context.PhpbbUsers.RemoveRange(await _context.PhpbbUsers.Where(u => u.UserId == userId).ToListAsync());
                _context.PhpbbUserTopicPostNumber.RemoveRange(await _context.PhpbbUserTopicPostNumber.Where(u => u.UserId == userId).ToListAsync());
                _context.PhpbbWarnings.RemoveRange(await _context.PhpbbWarnings.Where(u => u.UserId == userId).ToListAsync());
                _context.PhpbbZebra.RemoveRange(await _context.PhpbbZebra.Where(u => u.UserId == userId).ToListAsync());
                _context.PhpbbUsers.Remove(user);
            }

            try
            {
                var message = null as string;
                var isSuccess = null as bool?;

                switch (action)
                {
                    case AdminUserActions.Activate:
                        {
                            user.UserInactiveReason = UserInactiveReason.NotInactive;
                            user.UserInactiveTime = 0L;
                            message = $"Utilizatorul '{user.Username}' a fost activat.";
                            isSuccess = true;
                            break;
                        }
                    case AdminUserActions.Deactivate:
                        {
                            user.UserInactiveReason = UserInactiveReason.InactivatedByAdmin;
                            user.UserInactiveTime = DateTime.UtcNow.ToUnixTimestamp();
                            await flagUserAsChanged();
                            message = $"Utilizatorul '{user.Username}' a fost dezactivat.";
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

                            posts.ForEach(async p =>
                            {
                                p.PostUsername = user.Username;
                                p.PosterId = (await _userService.GetAnonymousDbUserAsync()).UserId;
                            });

                            await flagUserAsChanged();
                            await deleteUser();
                            message = $"Utilizatorul '{user.Username}' a fost șters iar mesajele păstrate.";
                            isSuccess = true;
                            break;
                        }
                    case AdminUserActions.Delete_DeleteMessages:
                        {
                            var toDelete = await _context.PhpbbPosts.Where(p => p.PosterId == userId).ToListAsync();
                            _context.PhpbbPosts.RemoveRange(toDelete);
                            await _context.SaveChangesAsync();
                            toDelete.ForEach(async p => await _postService.CascadePostDelete(_context, p, false));

                            await flagUserAsChanged();
                            await deleteUser();
                            message = $"Utilizatorul '{user.Username}' a fost șters cu tot cu mesajele scrise.";
                            isSuccess = true;
                            break;
                        }
                    default: throw new ArgumentException($"Unknown action '{action}'.", nameof(action));
                }

                await _context.SaveChangesAsync();

                return (message, isSuccess);
            }
            catch (Exception ex)
            {
                return ($"Acțiunea {action} nu a putut fi aplicată utilizatorului '{user.Username}'. Eroare: {ex.Message}", false);
            }
        }

        public async Task<List<PhpbbUsers>> UserSearchAsync(string username, string email, int? userid)
            => await (
                from u in _context.PhpbbUsers.AsNoTracking()
                where (string.IsNullOrWhiteSpace(username) || u.UsernameClean.Contains(_utils.CleanString(username), StringComparison.InvariantCultureIgnoreCase))
                    && (string.IsNullOrWhiteSpace(email) || u.UserEmail == email)
                    && ((userid ?? 0) == 0 || u.UserId == userid)
                select u
            ).ToListAsync();
    }
}
