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

        public async Task LogAdminUserAction(AdminUserActions action, int adminUserId, int userId)
            => await WithErrorHandling(async () =>
            {
                var user = await _context.Database.GetDbConnection().QueryFirstOrDefaultAsync<PhpbbUsers>("SELECT * FROM phpbb_users WHERE user_id = @userId", new { userId });
                if (user != null)
                {
                    await Log(action, $"User Id: {userId}, Username: {user.Username}", adminUserId);
                }
            });

        public async Task LogAdminForumAction(AdminForumActions action, int adminUserId, int forumId)
            => await WithErrorHandling(async () =>
            {
                var forum = await _context.Database.GetDbConnection().QueryFirstOrDefaultAsync<PhpbbForums>("SELECT * FROM phpbb_forums WHERE forum_id = @forumId", new { forumId });
                if (forum != null)
                {
                    await Log(action, $"Forum Id: {forumId}, Forum name: {forum.ForumName}", adminUserId, forumId);
                }
            });

        public async Task LogModeratorTopicAction(ModeratorTopicActions action, int modUserId, int topicId, string additionalData = null)
            => await WithErrorHandling(async () =>
            {
                var topic = await _context.Database.GetDbConnection().QueryFirstOrDefaultAsync<PhpbbTopics>("SELECT * FROM phpbb_topics WHERE topic_id = @topicId", new { topicId });
                if (topic != null)
                {
                    await Log(action, $"Topic Id: {topicId}, Topic title: {topic.TopicTitle}, Additional data: {additionalData}", modUserId, topic.ForumId, topicId);
                }
            });

        public async Task LogModeratorPostAction(ModeratorPostActions action, int modUserId, int postId, string additionalData = null)
            => await WithErrorHandling(async () =>
            {
                var post = await _context.Database.GetDbConnection().QueryFirstOrDefaultAsync<PhpbbPosts>("SELECT * FROM phpbb_posts WHERE post_id = @postId", new { postId });
                if (post != null)
                { 
                    await LogModeratorPostAction(action, modUserId, post, additionalData); 
                }
            });

        public async Task LogModeratorPostAction(ModeratorPostActions action, int modUserId, PhpbbPosts post, string additionalData = null)
            => await WithErrorHandling(async () => await Log(action, $"Post Id: {post.PostId}, Post subject: {post.PostSubject}, Additional data: {additionalData}", modUserId, post.ForumId, post.TopicId));

        public async Task<(List<OperationLogSummary> PageItems, int Count)> GetOperationLogs(OperationLogType? logType, string authorName = null, int page = 1)
            => await WithErrorHandling(async () =>
            {
                using var multi = await _context.Database.GetDbConnection().QueryMultipleAsync(
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
                return Directory.EnumerateFiles("logs", "log*.txt").Select(Parse).Where(x => x.LogDate != default && x.LogPath != default).ToList();
            });

        private async Task Log(Enum modAction, string logData, int userId, int forumId = 0, int topicId = 0)
        {
            await _context.Database.GetDbConnection().ExecuteAsync(
                "INSERT INTO phpbb_log (user_id, forum_id, topic_id, log_data, log_ip, log_operation, log_time, log_type) " +
                "VALUES (@userId, @forumId, @topicId, @logData, @logIp, @logOperation, @logTime, @logType)",
                new
                {
                    userId,
                    forumId,
                    topicId,
                    logData,
                    logIp = _httpContextAccessor.HttpContext.Connection.RemoteIpAddress.ToString(),
                    logOperation = modAction.ToString(),
                    logTime = DateTime.UtcNow.ToUnixTimestamp(),
                    logType = OperationLogType.Moderator
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
                _utils.HandleErrorAsWarning(ex, "Failed to save operation logs.");
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
                _utils.HandleErrorAsWarning(ex, "Failed to save operation logs.");
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
                _utils.HandleErrorAsWarning(ex, "Failed to save operation logs.");
                return default;
            }
        }
    }
}
