using Microsoft.Extensions.Configuration;
using PhpbbInDotnet.Forum.Contracts;
using PhpbbInDotnet.Forum.ForumDb;
using PhpbbInDotnet.Forum.ForumDb.Entities;
using PhpbbInDotnet.Forum.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace PhpbbInDotnet.Forum.Services
{
    public class AdminUserService
    {
        private readonly ForumDbContext _context;
        private readonly Utils _utils;
        private readonly PostService _postService;
        private readonly UserService _userService;
        private readonly CacheService _cacheService;
        private readonly IConfiguration _config;

        public AdminUserService(ForumDbContext context, Utils utils, PostService postService, UserService userService, CacheService cacheService, IConfiguration config)
        {
            _context = context;
            _utils = utils;
            _postService = postService;
            _userService = userService;
            _cacheService = cacheService;
            _config = config;
        }

        public async Task<List<PhpbbUsers>> GetInactiveUsers()
            => await (
                from u in _context.PhpbbUsers.AsNoTracking()
                where u.UserInactiveTime > 0
                    && u.UserInactiveReason != UserInactiveReason.NotInactive
                select u
            ).ToListAsync();

        public async Task<(string Message, bool? IsSuccess)> ManageUser(AdminUserActions? action, int? userId)
        {

            if (userId == Constants.ANONYMOUS_USER_ID)
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
                await _cacheService.SetInCache(key, true, TimeSpan.FromDays(_config.GetValue<int>("LoginSessionSlidingExpirationDays")));
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

                            posts.ForEach(p =>
                            {
                                p.PostUsername = user.Username;
                                p.PosterId = Constants.ANONYMOUS_USER_ID;
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
                return ($"Acțiunea {action} nu a putut fi aplicată utilizatorului '{user.Username}'. ID: {_utils.HandleError(ex)}.", false);
            }
        }

        public async Task<(string Message, bool? IsSuccess)> ManageRank(int? rankId, string rankName, bool? deleteRank)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(rankName))
                {
                    return ("Numele rangului este invalid", false);
                }
                if ((rankId ?? 0) == 0)
                {
                    var result = await _context.PhpbbRanks.AddAsync(new PhpbbRanks
                    {
                        RankTitle = rankName
                    });
                    result.Entity.RankId = 0;
                }
                else if (deleteRank ?? false)
                {
                    var actual = await _context.PhpbbRanks.FirstOrDefaultAsync(x => x.RankId == rankId);
                    if (actual == null)
                    {
                        return ($"Rangul {rankId} nu există.", false);
                    }
                    _context.PhpbbRanks.Remove(actual);
                }
                else
                {
                    var actual = await _context.PhpbbRanks.FirstOrDefaultAsync(x => x.RankId == rankId);
                    if (actual == null)
                    {
                        return ($"Rangul {rankId} nu există.", false);
                    }
                    actual.RankTitle = rankName;
                }
                await _context.SaveChangesAsync();
                return ("Rangul a fost actualizat cu succes!", true);
            }
            catch
            {
                return ("A intervenit o eroare, încearcă mai târziu", false);
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

        public async Task<(string Message, bool? IsSuccess)> ManageGroup(UpsertGroupDto dto)
        {
            try
            {
                void update(PhpbbGroups destination, UpsertGroupDto source)
                {
                    destination.GroupName = source.Name;
                    destination.GroupRank = source.Rank;
                    destination.GroupColour = source.DbColor;
                    destination.GroupUserUploadSize = source.UploadLimit * 1024 * 1024;
                    destination.GroupEditTime = source.EditTime;
                }

                async Task<bool> roleIsValid(int roleId) => await _context.PhpbbAclRoles.FirstOrDefaultAsync(x => x.RoleId == roleId) != null;

                var action = "";
                var changedColor = false;
                PhpbbGroups actual;
                if (dto.Id == 0)
                {
                    actual = new PhpbbGroups();
                    update(actual, dto);
                    var result = await _context.PhpbbGroups.AddAsync(actual);
                    result.Entity.GroupId = 0;
                    await _context.SaveChangesAsync();
                    actual = result.Entity;
                    action = "adăugat";
                }
                else
                {
                    actual = await _context.PhpbbGroups.FirstOrDefaultAsync(x => x.GroupId == dto.Id);
                    if (actual == null)
                    {
                        return ($"Grupul '{dto.Id}' nu există.", false);
                    }

                    if (dto.Delete ?? false)
                    {
                        if (await _context.PhpbbUsers.AsNoTracking().CountAsync(x => x.GroupId == dto.Id) > 0)
                        {
                            return ($"Grupul '{actual.GroupName}' nu poate fi șters deoarece nu este gol.", false);
                        }
                        _context.PhpbbGroups.Remove(actual);
                        actual = null;
                        action = "șters";
                    }
                    else
                    {
                        changedColor = !actual.GroupColour.Equals(dto.DbColor, StringComparison.InvariantCultureIgnoreCase);
                        update(actual, dto);
                        action = "actualizat";
                    }
                    await _context.SaveChangesAsync();
                }

                if (actual != null)
                {
                    var currentRole = await _context.PhpbbAclGroups.FirstOrDefaultAsync(x => x.GroupId == actual.GroupId && x.ForumId == 0);
                    if (currentRole != null)
                    {
                        if (dto.Role == 0)
                        {
                            _context.PhpbbAclGroups.Remove(currentRole);
                        }
                        else if (currentRole.AuthRoleId != dto.Role && await roleIsValid(dto.Role))
                        {
                            _context.PhpbbAclGroups.Remove(currentRole);
                            await _context.SaveChangesAsync();
                            currentRole = null;
                        }
                    }
                    if (currentRole == null && dto.Role != 0 && await roleIsValid(dto.Role))
                    {
                        var result = _context.PhpbbAclGroups.Add(new PhpbbAclGroups
                        {
                            GroupId = actual.GroupId,
                            AuthRoleId = dto.Role,
                            ForumId = 0
                        });
                        result.Entity.AuthOptionId = result.Entity.AuthSetting = 0;
                    }

                    await _context.SaveChangesAsync();
                }

                if (changedColor)
                {
                    var affectedUsers = await _context.PhpbbUsers.Where(x => x.GroupId == dto.Id).ToListAsync();
                    affectedUsers.ForEach(x => x.UserColour = dto.DbColor);
                    _context.PhpbbUsers.UpdateRange(affectedUsers);
                    var affectedTopics = await (
                        from t in _context.PhpbbTopics
                        join u in affectedUsers on t.TopicLastPosterId equals u.UserId
                        select t
                    ).ToListAsync();
                    affectedTopics.ForEach(t => t.TopicLastPosterColour = dto.DbColor);
                    _context.PhpbbTopics.UpdateRange(affectedTopics);
                    var affectedForums = await (
                        from t in _context.PhpbbForums
                        join u in affectedUsers on t.ForumLastPosterId equals u.UserId
                        select t
                    ).ToListAsync();
                    affectedForums.ForEach(t => t.ForumLastPosterColour = dto.DbColor);
                    _context.PhpbbForums.UpdateRange(affectedForums);

                    await _context.SaveChangesAsync();
                }

                return ($"Grupul a fost {action ?? "actualizat"} cu succes!", true);
            }
            catch (Exception ex)
            {
                return ($"A intervenit o eroare. ID: {_utils.HandleError(ex)}.", false);
            }
        }
    }
}
