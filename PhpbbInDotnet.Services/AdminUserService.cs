using Dapper;
using LazyCache;
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
using System.Globalization;
using System.Linq;
using System.Net.Mail;
using System.Threading.Tasks;

namespace PhpbbInDotnet.Services
{
    public class AdminUserService : MultilingualServiceBase
    {
        private readonly ForumDbContext _context;
        private readonly PostService _postService;
        private readonly IAppCache _cache;
        private readonly IConfiguration _config;

        public AdminUserService(ForumDbContext context, PostService postService, IAppCache cache, IConfiguration config, 
            CommonUtils utils, LanguageProvider languageProvider, IHttpContextAccessor httpContextAccessor)
            : base(utils, languageProvider, httpContextAccessor)
        {
            _context = context;
            _postService = postService;
            _cache = cache;
            _config = config;
        }

        public async Task<List<PhpbbUsers>> GetInactiveUsers()
            => await (
                from u in _context.PhpbbUsers.AsNoTracking()
                where u.UserInactiveTime > 0
                    && u.UserInactiveReason != UserInactiveReason.NotInactive
                orderby u.UserInactiveTime descending
                select u
            ).ToListAsync();

        public async Task<(string Message, bool? IsSuccess)> DeleteUsersWithEmailNotConfirmed(int[] userIds)
        {
            var lang = await GetLanguage();
            if (!(userIds?.Any() ?? false))
            {
                return (LanguageProvider.Admin[lang, "NO_USER_SELECTED"], null);
            }

            try
            {
                var users = await (
                    from u in _context.PhpbbUsers
                    where userIds.Contains(u.UserId) && u.UserInactiveReason == UserInactiveReason.NewlyRegisteredNotConfirmed
                    select u
                ).ToListAsync();

                _context.PhpbbUsers.RemoveRange(users);
                await _context.SaveChangesAsync();

                if (users.Count == userIds.Length)
                {
                    return (LanguageProvider.Admin[lang, "USERS_DELETED_SUCCESSFULLY"], true);
                }

                var dbUserIds = users.Select(u => u.UserId).ToList();
                var changedStatus = userIds.Where(u => !dbUserIds.Contains(u));

                return (
                    string.Format(
                        LanguageProvider.Admin[lang, "USERS_DELETED_PARTIALLY_FORMAT"], 
                        string.Join(", ", dbUserIds), 
                        LanguageProvider.Enums[lang, UserInactiveReason.NewlyRegisteredNotConfirmed], 
                        string.Join(", ", changedStatus)
                    ), 
                    null
                );
            }
            catch (Exception ex)
            {
                var id = Utils.HandleError(ex);
                return (string.Format(LanguageProvider.Errors[lang, "AN_ERROR_OCCURRED_TRY_AGAIN_ID_FORMAT"], id), false);
            }
        }

        public async Task<(string Message, bool? IsSuccess)> ManageUser(AdminUserActions? action, int? userId, PageContext pageContext, HttpContext httpContext)
        {
            var lang = await GetLanguage();
            if (userId == Constants.ANONYMOUS_USER_ID)
            {
                return (LanguageProvider.Admin[lang, "CANT_DELETE_ANONYMOUS_USER"], false);
            }

            var user = await _context.PhpbbUsers.FirstOrDefaultAsync(u => u.UserId == userId);
            if (user == null)
            {
                return (string.Format(LanguageProvider.Admin[lang, "USER_DOESNT_EXIST_FORMAT"], userId ?? 0), false);
            }

            void flagUserAsChanged()
            {
                var key = $"UserMustLogIn_{user.UsernameClean}";
                _cache.Add(key, true, TimeSpan.FromDays(_config.GetValue<int>("LoginSessionSlidingExpirationDays")));
            }

            async Task deleteUser()
            {
                _context.PhpbbAclUsers.RemoveRange(_context.PhpbbAclUsers.Where(u => u.UserId == userId));
                _context.PhpbbBanlist.RemoveRange(_context.PhpbbBanlist.Where(u => u.BanUserid == userId));
                _context.PhpbbBookmarks.RemoveRange(_context.PhpbbBookmarks.Where(u => u.UserId == userId));
                _context.PhpbbBots.RemoveRange(_context.PhpbbBots.Where(u => u.UserId == userId));
                _context.PhpbbDrafts.RemoveRange(_context.PhpbbDrafts.Where(u => u.UserId == userId));
                _context.PhpbbForumsAccess.RemoveRange(_context.PhpbbForumsAccess.Where(u => u.UserId == userId));
                _context.PhpbbForumsTrack.RemoveRange(_context.PhpbbForumsTrack.Where(u => u.UserId == userId));
                _context.PhpbbForumsWatch.RemoveRange(_context.PhpbbForumsWatch.Where(u => u.UserId == userId));
                _context.PhpbbLog.RemoveRange(_context.PhpbbLog.Where(u => u.UserId == userId));
                _context.PhpbbModeratorCache.RemoveRange(_context.PhpbbModeratorCache.Where(u => u.UserId == userId));
                await _context.Database.GetDbConnection().ExecuteAsync("DELETE FROM phpbb_poll_votes WHERE vote_user_id = @userId", new { userId });
                _context.PhpbbPrivmsgsFolder.RemoveRange(_context.PhpbbPrivmsgsFolder.Where(u => u.UserId == userId));
                _context.PhpbbPrivmsgsRules.RemoveRange(_context.PhpbbPrivmsgsRules.Where(u => u.UserId == userId));
                _context.PhpbbPrivmsgsTo.RemoveRange(_context.PhpbbPrivmsgsTo.Where(u => u.UserId == userId));
                _context.PhpbbProfileFieldsData.RemoveRange(_context.PhpbbProfileFieldsData.Where(u => u.UserId == userId));
                _context.PhpbbReports.RemoveRange(_context.PhpbbReports.Where(u => u.UserId == userId));
                _context.PhpbbSessions.RemoveRange(_context.PhpbbSessions.Where(u => u.SessionUserId == userId));
                _context.PhpbbSessionsKeys.RemoveRange(_context.PhpbbSessionsKeys.Where(u => u.UserId == userId));
                _context.PhpbbTopicsPosted.RemoveRange(_context.PhpbbTopicsPosted.Where(u => u.UserId == userId));
                _context.PhpbbTopicsTrack.RemoveRange(_context.PhpbbTopicsTrack.Where(u => u.UserId == userId));
                _context.PhpbbTopicsWatch.RemoveRange(_context.PhpbbTopicsWatch.Where(u => u.UserId == userId));
                _context.PhpbbUserGroup.RemoveRange(_context.PhpbbUserGroup.Where(u => u.UserId == userId));
                _context.PhpbbUsers.RemoveRange(_context.PhpbbUsers.Where(u => u.UserId == userId));
                _context.PhpbbUserTopicPostNumber.RemoveRange(_context.PhpbbUserTopicPostNumber.Where(u => u.UserId == userId));
                _context.PhpbbWarnings.RemoveRange(_context.PhpbbWarnings.Where(u => u.UserId == userId));
                _context.PhpbbZebra.RemoveRange(_context.PhpbbZebra.Where(u => u.UserId == userId));
                _context.PhpbbUsers.Remove(user);
            }

            try
            {
                string message = null;
                bool? isSuccess = null;
                var forumName = _config.GetValue<string>("ForumName");
                switch (action)
                {
                    case AdminUserActions.Activate:
                        {
                            using var emailMessage = new MailMessage
                            {
                                From = new MailAddress($"admin@metrouusor.com", forumName),
                                Subject = string.Format(LanguageProvider.Email[user.UserLang, "ACCOUNT_ACTIVATED_NOTIFICATION_SUBJECT_FORMAT"], forumName),
                                Body = await Utils.RenderRazorViewToString(
                                    "_AccountActivatedNotification", 
                                    new AccountActivatedNotificationDto 
                                    { 
                                        Username = user.Username,
                                        Language = user.UserLang
                                    }, 
                                    pageContext, 
                                    httpContext
                                ),
                                IsBodyHtml = true
                            };
                            emailMessage.To.Add(user.UserEmail);
                            await Utils.SendEmail(emailMessage);

                            user.UserInactiveReason = UserInactiveReason.NotInactive;
                            user.UserInactiveTime = 0L;
                            user.UserReminded = 0;
                            user.UserRemindedTime = 0L;
                            message = string.Format(LanguageProvider.Admin[lang, "USER_ACTIVATED_FORMAT"], user.Username);
                            isSuccess = true;
                            break;
                        }
                    case AdminUserActions.Deactivate:
                        {
                            user.UserInactiveReason = UserInactiveReason.InactivatedByAdmin;
                            user.UserInactiveTime = DateTime.UtcNow.ToUnixTimestamp();
                            flagUserAsChanged();
                            message = string.Format(LanguageProvider.Admin[lang, "USER_DEACTIVATED_FORMAT"], user.Username);
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

                            flagUserAsChanged();
                            await deleteUser();
                            message = string.Format(LanguageProvider.Admin[lang, "USER_DELETED_POSTS_KEPT_FORMAT"], user.Username);
                            isSuccess = true;
                            break;
                        }
                    case AdminUserActions.Delete_DeleteMessages:
                        {
                            var toDelete = await _context.PhpbbPosts.Where(p => p.PosterId == userId).ToListAsync();
                            _context.PhpbbPosts.RemoveRange(toDelete);
                            await _context.SaveChangesAsync();
                            toDelete.ForEach(async p => await _postService.CascadePostDelete(p, false));

                            flagUserAsChanged();
                            await deleteUser();
                            message = string.Format(LanguageProvider.Admin[lang, "USER_DELETED_POSTS_DELETED_FORMAT"], user.Username);
                            isSuccess = true;
                            break;
                        }
                    case AdminUserActions.Remind:
                        {
                            string subject;
                            WelcomeEmailDto model;
                            if (user.UserInactiveReason == UserInactiveReason.NewlyRegisteredNotConfirmed)
                            {
                                subject = string.Format(LanguageProvider.Email[lang, "WELCOME_REMINDER_SUBJECT_FORMAT"], forumName);
                                model = new WelcomeEmailDto
                                {
                                    RegistrationCode = user.UserActkey,
                                    Subject = subject,
                                    UserName = user.Username,
                                    IsRegistrationReminder = true,
                                    RegistrationDate = user.UserRegdate.ToUtcTime()
                                };
                            }
                            else if (user.UserInactiveReason == UserInactiveReason.ChangedEmailNotConfirmed)
                            {
                                subject = string.Format(LanguageProvider.Email[lang, "EMAIL_CHANGED_REMINDER_SUBJECT_FORMAT"], forumName);
                                model = new WelcomeEmailDto
                                {
                                    RegistrationCode = user.UserActkey,
                                    Subject = subject,
                                    UserName = user.Username,
                                    IsEmailChangeReminder = true,
                                    EmailChangeDate = user.UserInactiveTime.ToUtcTime()
                                };
                            }
                            else
                            {
                                message = string.Format(LanguageProvider.Admin[lang, "CANT_REMIND_INVALID_USER_STATE_FORMAT"], user.Username, LanguageProvider.Enums[lang, user.UserInactiveReason]);
                                isSuccess = false;
                                break;
                            }

                            using var emailMessage = new MailMessage
                            {
                                From = new MailAddress($"admin@metrouusor.com", forumName),
                                Subject = subject,
                                Body = await Utils.RenderRazorViewToString("_WelcomeEmailPartial", model, pageContext, httpContext),
                                IsBodyHtml = true
                            };
                            emailMessage.To.Add(user.UserEmail);
                            await Utils.SendEmail(emailMessage);
                            user.UserReminded = 1;
                            user.UserRemindedTime = DateTime.UtcNow.ToUnixTimestamp();
                            message = string.Format(LanguageProvider.Admin[lang, "USER_REMINDED_FORMAT"], user.Username);
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
                var id = Utils.HandleError(ex);
                return (string.Format(LanguageProvider.Errors[lang, "AN_ERROR_OCCURRED_TRY_AGAIN_ID_FORMAT"], id), false);
            }
        }

        public async Task<(string Message, bool? IsSuccess)> ManageRank(int? rankId, string rankName, bool? deleteRank)
        {
            var lang = await GetLanguage();
            try
            {
                if (string.IsNullOrWhiteSpace(rankName))
                {
                    return (LanguageProvider.Admin[lang, "INVALID_RANK_NAME"], false);
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
                        return (string.Format(LanguageProvider.Admin[lang, "RANK_DOESNT_EXIST_FORMAT"], rankId), false);
                    }
                    _context.PhpbbRanks.Remove(actual);
                }
                else
                {
                    var actual = await _context.PhpbbRanks.FirstOrDefaultAsync(x => x.RankId == rankId);
                    if (actual == null)
                    {
                        return (string.Format(LanguageProvider.Admin[lang, "RANK_DOESNT_EXIST_FORMAT"], rankId), false);
                    }
                    actual.RankTitle = rankName;
                }
                await _context.SaveChangesAsync();
                return (LanguageProvider.Admin[lang, "RANK_UPDATED_SUCCESSFULLY"], true);
            }
            catch (Exception ex)
            {
                var id = Utils.HandleError(ex);
                return (string.Format(LanguageProvider.Errors[lang, "AN_ERROR_OCCURRED_TRY_AGAIN_ID_FORMAT"], id), false);
            }
        }

        public async Task<(string Message, bool IsSuccess, List<PhpbbUsers> Result)> UserSearchAsync(AdminUserSearch searchParameters)
        {
            long ParseDate(string value, bool isUpperLimit)
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    return isUpperLimit ? DateTime.UtcNow.ToUnixTimestamp() : 0L;
                }

                try
                {
                    var toReturn = DateTime.ParseExact(value, "dd.MM.yyyy", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal);
                    return (isUpperLimit ? toReturn.AddDays(1).AddMilliseconds(-1) : toReturn).ToUnixTimestamp();
                }
                catch (Exception ex)
                {
                    throw new DateInputException(ex);
                }
            }

            var lang = await GetLanguage();
            try
            {
                var rf = ParseDate(searchParameters?.RegisteredFrom, false);
                var rt = ParseDate(searchParameters?.RegisteredTo, true);
                var username = searchParameters?.Username;
                var email = searchParameters?.Email;
                var userId = searchParameters?.UserId ?? 0;
                var query = from u in _context.PhpbbUsers.AsNoTracking()
                            where (string.IsNullOrWhiteSpace(username) || u.UsernameClean.Contains(Utils.CleanString(username)))
                                && (string.IsNullOrWhiteSpace(email) || u.UserEmailHash == Utils.CalculateCrc32Hash(email))
                                && (userId == 0 || u.UserId == userId)
                                && u.UserRegdate >= rf && u.UserRegdate <= rt
                                && u.UserId != Constants.ANONYMOUS_USER_ID
                            join ug in _context.PhpbbUserGroup.AsNoTracking()
                            on u.UserId equals ug.UserId into joined
                            from j in joined.DefaultIfEmpty()
                            let groupId = j == null ? u.GroupId : j.GroupId
                            where groupId != Constants.BOTS_GROUP_ID && groupId != Constants.GUESTS_GROUP_ID
                            select u;

                if (!(searchParameters?.NeverActive ?? false))
                {
                    var laf = ParseDate(searchParameters?.LastActiveFrom, false);
                    var lat = ParseDate(searchParameters?.LastActiveTo, true);
                    query = from q in query
                            where q.UserLastvisit >= laf && q.UserLastvisit <= lat
                            select q;
                }
                else
                {
                    query = from q in query
                            where q.UserLastvisit == 0L
                            select q;
                }

                query = (
                    from q in query
                    orderby q.UsernameClean ascending
                    select q
                ).Distinct();

                return (null, true, await query.ToListAsync());
            }
            catch (DateInputException die)
            {
                Utils.HandleError(die.InnerException, die.Message);
                return (LanguageProvider.Admin[lang, "ONE_OR_MORE_INVALID_INPUT_DATES"], false, new List<PhpbbUsers>());
            }
            catch (Exception ex)
            {
                var id = Utils.HandleError(ex);
                return (string.Format(LanguageProvider.Errors[lang, "AN_ERROR_OCCURRED_TRY_AGAIN_ID_FORMAT"], id), false, new List<PhpbbUsers>());
            }
        }
        public async Task<(string Message, bool? IsSuccess)> ManageGroup(UpsertGroupDto dto)
        {
            var lang = await GetLanguage();
            try
            {
                void update(PhpbbGroups destination, UpsertGroupDto source)
                {
                    destination.GroupName = source.Name;
                    destination.GroupDesc = source.Desc;
                    destination.GroupRank = source.Rank;
                    destination.GroupColour = source.DbColor;
                    destination.GroupUserUploadSize = source.UploadLimit * 1024 * 1024;
                    destination.GroupEditTime = source.EditTime;
                }

                async Task<bool> roleIsValid(int roleId) => await _context.PhpbbAclRoles.FirstOrDefaultAsync(x => x.RoleId == roleId) != null;

                AdminGroupActions? action = null;
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
                    action = AdminGroupActions.Add;
                }
                else
                {
                    actual = await _context.PhpbbGroups.FirstOrDefaultAsync(x => x.GroupId == dto.Id);
                    if (actual == null)
                    {
                        return (string.Format(LanguageProvider.Admin[lang, "GROUP_DOESNT_EXIST"], dto.Id), false);
                    }

                    if (dto.Delete ?? false)
                    {
                        if (await _context.PhpbbUsers.AsNoTracking().CountAsync(x => x.GroupId == dto.Id) > 0)
                        {
                            return (string.Format(LanguageProvider.Admin[lang, "CANT_DELETE_NOT_EMPTY_FORMAT"], actual.GroupName), false);
                        }
                        _context.PhpbbGroups.Remove(actual);
                        actual = null;
                        action = AdminGroupActions.Delete;
                    }
                    else
                    {
                        changedColor = !actual.GroupColour.Equals(dto.DbColor, StringComparison.InvariantCultureIgnoreCase);
                        update(actual, dto);
                        action = AdminGroupActions.Update;
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
                        where affectedUsers.Select(u => u.UserId).Contains(t.TopicLastPosterId)
                        select t
                    ).ToListAsync();
                    affectedTopics.ForEach(t => t.TopicLastPosterColour = dto.DbColor);
                    _context.PhpbbTopics.UpdateRange(affectedTopics);
                    var affectedForums = await (
                        from t in _context.PhpbbForums
                        where affectedUsers.Select(u => u.UserId).Contains(t.ForumLastPosterId)
                        select t
                    ).ToListAsync();
                    affectedForums.ForEach(t => t.ForumLastPosterColour = dto.DbColor);
                    _context.PhpbbForums.UpdateRange(affectedForums);

                    await _context.SaveChangesAsync();
                }

                var message = action switch
                {
                    AdminGroupActions.Add => LanguageProvider.Admin[lang, "GROUP_ADDED_SUCCESSFULLY"],
                    AdminGroupActions.Delete => LanguageProvider.Admin[lang, "GROUP_DELETED_SUCCESSFULLY"],
                    AdminGroupActions.Update => LanguageProvider.Admin[lang, "GROUP_UPDATED_SUCCESSFULLY"],
                    _ => LanguageProvider.Admin[lang, "GROUP_UPDATED_SUCCESSFULLY"],
                };
                return (message, true);
            }
            catch (Exception ex)
            {
                var id = Utils.HandleError(ex);
                return (string.Format(LanguageProvider.Errors[lang, "AN_ERROR_OCCURRED_TRY_AGAIN_ID_FORMAT"], id), false);
            }
        }

        public async Task<(string Message, bool? IsSuccess)> BanUser(List<PhpbbBanlist> banlist, List<int> indexesToRemove)
        {
            var lang = await GetLanguage();
            try
            {
                await _context.PhpbbBanlist.AddRangeAsync(banlist.Where(x => x.BanId == 0));
                _context.PhpbbBanlist.UpdateRange(banlist.Where(x => x.BanId != 0));
                await _context.SaveChangesAsync();
                await _context.Database.GetDbConnection().ExecuteAsync("DELETE FROM phpbb_banlist WHERE ban_id IN @ids", new { ids = indexesToRemove.Select(idx => banlist[idx].BanId) });

                return (LanguageProvider.Admin[lang, "BANLIST_UPDATED_SUCCESSFULLY"], true);
            }
            catch (Exception ex)
            {
                var id = Utils.HandleError(ex);
                return (string.Format(LanguageProvider.Errors[lang, "AN_ERROR_OCCURRED_TRY_AGAIN_ID_FORMAT"], id), false);
            }
        }

        class DateInputException : Exception
        {
            internal DateInputException(Exception inner) : base("Failed to parse exact input dates", inner) 
            {
            
            }
        }
    }
}
