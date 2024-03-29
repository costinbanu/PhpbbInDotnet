﻿using Dapper;
using Microsoft.AspNetCore.Http;
using PhpbbInDotnet.Database.Entities;
using PhpbbInDotnet.Database.SqlExecuter;
using PhpbbInDotnet.Domain;
using PhpbbInDotnet.Domain.Extensions;
using PhpbbInDotnet.Domain.Utilities;
using PhpbbInDotnet.Objects;
using Serilog;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PhpbbInDotnet.Services
{
    class OperationLogService : IOperationLogService
    {
        public int LogPageSize => 100;

        private readonly ISqlExecuter _sqlExecuter;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILogger _logger;

        public OperationLogService(ISqlExecuter sqlExecuter, IHttpContextAccessor httpContextAccessor, ILogger logger)
        {
            _sqlExecuter = sqlExecuter;
            _httpContextAccessor = httpContextAccessor;
            _logger = logger;
        }

        public async Task<(List<OperationLogSummary> PageItems, int Count)> GetOperationLogs(OperationLogType? logType, string? authorName = null, int page = 1)
            => await WithErrorHandling(async () =>
            {
                var logs = await _sqlExecuter.WithPagination(LogPageSize * (page - 1), LogPageSize).QueryAsync<OperationLogSummary>(
                    @"SELECT l.user_id, u.username, l.forum_id, f.forum_name, l.topic_id, t.topic_title, l.log_type, l.log_operation, l.log_data, l.log_time
                        FROM phpbb_log l
                        LEFT JOIN phpbb_users u ON l.user_id = u.user_id
                        LEFT JOIN phpbb_forums f ON l.forum_id = f.forum_id
                        LEFT JOIN phpbb_topics t ON l.topic_id = t.topic_id
                       WHERE (@logType IS NULL OR l.log_type = @logType) 
                         AND (@authorName = '' OR u.username_clean = @authorName)
                       ORDER BY l.log_time DESC",
                    new
                    {
                        logType,
                        authorName = StringUtility.CleanString(authorName)
                    });
                var count = await _sqlExecuter.ExecuteScalarAsync<int>(
                    @"SELECT count(*)
                        FROM phpbb_log l
                        LEFT JOIN phpbb_users u ON l.user_id = u.user_id
                       WHERE (@logType IS NULL OR l.log_type = @logType) 
                         AND (@authorName = '' OR u.username_clean = @authorName)",
                    new
                    {
                        logType,
                        authorName = StringUtility.CleanString(authorName)
                    });
                return (logs.AsList(), count);
            });

        public async Task LogAdminUserAction(AdminUserActions action, int adminUserId, PhpbbUsers user, string? additionalData = null)
            => await WithErrorHandling(async () =>
                await Log(EnumUtility.ExpandEnum(action), $"User Id: {user.UserId}, Username: {user.Username}, Additional data: {additionalData}", adminUserId, OperationLogType.Administrator)
            );

        public async Task LogAdminRankAction(AdminRankActions action, int adminUserId, PhpbbRanks rank)
            => await WithErrorHandling(async () =>
                await Log(EnumUtility.ExpandEnum(action), $"Rank id: {rank.RankId}, Rank name: {rank.RankTitle}", adminUserId, OperationLogType.Administrator)
            );

        public async Task LogAdminGroupAction(AdminGroupActions action, int adminUserId, PhpbbGroups group)
            => await WithErrorHandling(async () =>
                await Log(EnumUtility.ExpandEnum(action), $"Group id: {group.GroupId}, Group name: {group.GroupName}", adminUserId, OperationLogType.Administrator)
            );

        public async Task LogAdminBanListAction(AdminBanListActions action, int adminUserId, UpsertBanListDto banList)
            => await WithErrorHandling(async () =>
                await Log(EnumUtility.ExpandEnum(action), $"Ban list id: {banList.BanId}, Ban list email: {banList.BanEmail}, Ban list IP: {banList.BanIp}", adminUserId, OperationLogType.Administrator)
            );

        public async Task LogAdminForumAction(AdminForumActions action, int adminUserId, PhpbbForums forum)
            => await WithErrorHandling(async () =>
                await Log(EnumUtility.ExpandEnum(action), $"Forum Id: {forum.ForumId}, Forum name: {forum.ForumName}", adminUserId, OperationLogType.Administrator, forum.ForumId)
            );

        public async Task LogModeratorTopicAction(ModeratorTopicActions action, int modUserId, int topicId, string? additionalData = null, ITransactionalSqlExecuter? transaction = null)
            => await WithErrorHandling(async () =>
            {
                var topic = await (transaction ?? _sqlExecuter).QueryFirstOrDefaultAsync<PhpbbTopics>("SELECT * FROM phpbb_topics WHERE topic_id = @topicId", new { topicId });
                if (topic != null)
                {
                    await Log(EnumUtility.ExpandEnum(action), $"Topic Id: {topicId}, Topic title: {topic.TopicTitle}, Additional data: {additionalData}", modUserId, OperationLogType.Moderator, topic.ForumId, topicId);
                }
            });

        public async Task LogModeratorPostAction(ModeratorPostActions action, int modUserId, int postId, string? additionalData = null, ITransactionalSqlExecuter? transaction = null)
            => await WithErrorHandling(async () =>
            {
                var post = await (transaction ?? _sqlExecuter).QueryFirstOrDefaultAsync<PhpbbPosts>("SELECT * FROM phpbb_posts WHERE post_id = @postId", new { postId });
                if (post != null)
                {
                    await LogModeratorPostAction(action, modUserId, post, additionalData);
                }
            });

        public async Task LogModeratorPostAction(ModeratorPostActions action, int modUserId, PhpbbPosts post, string? additionalData = null, ITransactionalSqlExecuter? transaction = null)
            => await WithErrorHandling(async () => await Log(EnumUtility.ExpandEnum(action), $"Post Id: {post.PostId}, Post subject: {post.PostSubject}, Additional data: {additionalData}", modUserId, OperationLogType.Moderator, post.ForumId, post.TopicId, transaction));

        public async Task LogUserProfileAction(UserProfileActions action, int editingUser, PhpbbUsers targetUser, string? additionalData = null)
            => await WithErrorHandling(async () =>
                await Log(EnumUtility.ExpandEnum(action), $"User {editingUser} has changed the profile of user {targetUser.UserId} ({targetUser.UsernameClean}), Additional data: {additionalData}", editingUser, OperationLogType.User)
            );

        private async Task Log(string action, string logData, int userId, OperationLogType operationType, int forumId = 0, int topicId = 0, ITransactionalSqlExecuter? transaction = null)
        {
            await (transaction ?? _sqlExecuter).ExecuteAsync(
                "INSERT INTO phpbb_log (user_id, forum_id, topic_id, log_data, log_ip, log_operation, log_time, log_type) " +
                "VALUES (@userId, @forumId, @topicId, @logData, @logIp, @logOperation, @logTime, @logType)",
                new
                {
                    userId,
                    forumId,
                    topicId,
                    logData,
                    logIp = _httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString() ?? string.Empty,
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
                _logger.WarningWithId(ex);
            }
        }

        private async Task<T?> WithErrorHandling<T>(Func<Task<T>> toDo)
        {
            try
            {
                return await toDo();
            }
            catch (Exception ex)
            {
                _logger.WarningWithId(ex);
                return default;
            }
        }
    }
}
