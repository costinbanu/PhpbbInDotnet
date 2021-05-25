using Dapper;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using PhpbbInDotnet.Database;
using PhpbbInDotnet.Database.Entities;
using PhpbbInDotnet.Objects;
using PhpbbInDotnet.Utilities;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace PhpbbInDotnet.Services
{
    public class OperationLogService
    {
        public int LOG_PAGE_SIZE => 100;

        private readonly ForumDbContext _context;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly CommonUtils _utils;

        public OperationLogService(ForumDbContext context, IHttpContextAccessor httpContextAccessor, CommonUtils utils)
        {
            _context = context;
            _httpContextAccessor = httpContextAccessor;
            _utils = utils;
        }

        public async Task<(List<OperationLogSummary> PageItems, int Count)> GetOperationLogs(OperationLogType? logType, string authorName = null, int page = 1)
            => await WithErrorHandling(async () =>
            {
                using var multi = await (await _context.GetDbConnectionAsync()).QueryMultipleAsync(
                    @"SELECT l.user_id, u.username, l.forum_id, f.forum_name, l.topic_id, t.topic_title, l.log_type, l.log_operation, l.log_data, l.log_time
                        FROM phpbb_log l
                        LEFT JOIN phpbb_users u ON l.user_id = u.user_id
                        LEFT JOIN phpbb_forums f ON l.forum_id = f.forum_id
                        LEFT JOIN phpbb_topics t ON l.topic_id = t.topic_id
                       WHERE (@logType IS NULL OR l.log_type = @logType) AND (@authorName = '' OR u.username_clean = @authorName)
                       ORDER BY l.log_time DESC
                       LIMIT @skip, @take;

                      SELECT count(*)
                        FROM phpbb_log l
                        LEFT JOIN phpbb_users u ON l.user_id = u.user_id
                       WHERE (@logType IS NULL OR l.log_type = @logType) AND (@authorName = '' OR u.username_clean = @authorName)",
                    new { logType, skip = LOG_PAGE_SIZE * (page - 1), take = LOG_PAGE_SIZE, authorName = _utils.CleanString(authorName) }
                );
                return ((await multi.ReadAsync<OperationLogSummary>()).AsList(), unchecked((int)await multi.ReadSingleAsync<long>()));
            });

        public List<(DateTime LogDate, string LogPath)> GetSystemLogs()
            => WithErrorHandling(() =>
            {
                static (DateTime LogDate, string LogPath) Parse(string path)
                {
                    if (DateTime.TryParseExact(Path.GetFileNameWithoutExtension(path)[3..], "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
                    {
                        return (date, path);
                    }
                    return (default, default);
                }
                return (
                    from f in Directory.EnumerateFiles("logs", "log*.txt")
                    let parsed = Parse(f)
                    where parsed.LogDate != default && parsed.LogPath != default && parsed.LogDate < DateTime.Today
                    orderby parsed.LogDate descending
                    select parsed
                ).ToList();
            });


        public async Task LogAdminUserAction(AdminUserActions action, int adminUserId, PhpbbUsers user, string additionalData = null)
            => await WithErrorHandling(async () =>
                await Log(_utils.EnumString(action), $"User Id: {user.UserId}, Username: {user.Username}, Additional data: {additionalData}", adminUserId, OperationLogType.Administrator)
            );

        public async Task LogAdminRankAction(AdminRankActions action, int adminUserId, PhpbbRanks rank)
            => await WithErrorHandling(async () =>
                await Log(_utils.EnumString(action), $"Rank id: {rank.RankId}, Rank name: {rank.RankTitle}", adminUserId, OperationLogType.Administrator)
            );

        public async Task LogAdminGroupAction(AdminGroupActions action, int adminUserId, PhpbbGroups group)
            => await WithErrorHandling(async () =>
                await Log(_utils.EnumString(action), $"Group id: {group.GroupId}, Group name: {group.GroupName}", adminUserId, OperationLogType.Administrator)
            );

        public async Task LogAdminBanListAction(AdminBanListActions action, int adminUserId, UpsertBanListDto banList)
            => await WithErrorHandling(async () =>
                await Log(_utils.EnumString(action), $"Ban list id: {banList.BanId}, Ban list email: {banList.BanEmail}, Ban list IP: {banList.BanIp}", adminUserId, OperationLogType.Administrator)
            );

        public async Task LogAdminForumAction(AdminForumActions action, int adminUserId, PhpbbForums forum)
            => await WithErrorHandling(async () =>
                await Log(_utils.EnumString(action), $"Forum Id: {forum.ForumId}, Forum name: {forum.ForumName}", adminUserId, OperationLogType.Administrator, forum.ForumId)
            );

        public async Task LogModeratorTopicAction(ModeratorTopicActions action, int modUserId, int topicId, string additionalData = null)
            => await WithErrorHandling(async () =>
            {
                var topic = await (await _context.GetDbConnectionAsync()).QueryFirstOrDefaultAsync<PhpbbTopics>("SELECT * FROM phpbb_topics WHERE topic_id = @topicId", new { topicId });
                if (topic != null)
                {
                    await Log(_utils.EnumString(action), $"Topic Id: {topicId}, Topic title: {topic.TopicTitle}, Additional data: {additionalData}", modUserId, OperationLogType.Moderator, topic.ForumId, topicId);
                }
            });

        public async Task LogModeratorPostAction(ModeratorPostActions action, int modUserId, int postId, string additionalData = null)
            => await WithErrorHandling(async () =>
            {
                var post = await (await _context.GetDbConnectionAsync()).QueryFirstOrDefaultAsync<PhpbbPosts>("SELECT * FROM phpbb_posts WHERE post_id = @postId", new { postId });
                if (post != null)
                { 
                    await LogModeratorPostAction(action, modUserId, post, additionalData); 
                }
            });

        public async Task LogModeratorPostAction(ModeratorPostActions action, int modUserId, PhpbbPosts post, string additionalData = null)
            => await WithErrorHandling(async () => await Log(_utils.EnumString(action), $"Post Id: {post.PostId}, Post subject: {post.PostSubject}, Additional data: {additionalData}", modUserId, OperationLogType.Moderator, post.ForumId, post.TopicId));

        public async Task LogUserProfileAction(UserProfileActions action, int editingUser, PhpbbUsers targetUser, string additionalData = null)
            => await WithErrorHandling(async () =>
                await Log(_utils.EnumString(action), $"User {editingUser} has changed the profile of user {targetUser.UserId} ({targetUser.UsernameClean}), Additional data: {additionalData}", editingUser, OperationLogType.User)
            );

        private async Task Log(string action, string logData, int userId, OperationLogType operationType, int forumId = 0, int topicId = 0)
        {
            await (await _context.GetDbConnectionAsync()).ExecuteAsync(
                "INSERT INTO phpbb_log (user_id, forum_id, topic_id, log_data, log_ip, log_operation, log_time, log_type) " +
                "VALUES (@userId, @forumId, @topicId, @logData, @logIp, @logOperation, @logTime, @logType)",
                new
                {
                    userId,
                    forumId,
                    topicId,
                    logData,
                    logIp = _httpContextAccessor.HttpContext.Connection.RemoteIpAddress.ToString(),
                    logOperation = action,
                    logTime = DateTime.UtcNow.ToUnixTimestamp(),
                    logType = (int)operationType
                }
            );
        }

        private async Task WithErrorHandling(Func<Task> toDo)
        {
            try
            {
                await toDo();
            }
            catch (Exception ex)
            {
                _utils.HandleErrorAsWarning(ex);
            }
        }

        private async Task<T> WithErrorHandling<T>(Func<Task<T>> toDo)
        {
            try
            {
                return await toDo();
            }
            catch (Exception ex)
            {
                _utils.HandleErrorAsWarning(ex);
                return default;
            }
        }

        private T WithErrorHandling<T>(Func<T> toDo)
        {
            try
            {
                return toDo();
            }
            catch (Exception ex)
            {
                _utils.HandleErrorAsWarning(ex);
                return default;
            }
        }
    }
}
