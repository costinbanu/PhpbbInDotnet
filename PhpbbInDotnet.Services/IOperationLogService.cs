using PhpbbInDotnet.Database.Entities;
using PhpbbInDotnet.Database.SqlExecuter;
using PhpbbInDotnet.Domain;
using PhpbbInDotnet.Objects;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PhpbbInDotnet.Services
{
    public interface IOperationLogService
    {
        int LogPageSize { get; }

        Task<(List<OperationLogSummary> PageItems, int Count)> GetOperationLogs(OperationLogType? logType, string? authorName = null, int page = 1);
        Task LogAdminBanListAction(AdminBanListActions action, int adminUserId, UpsertBanListDto banList);
        Task LogAdminForumAction(AdminForumActions action, int adminUserId, PhpbbForums forum);
        Task LogAdminGroupAction(AdminGroupActions action, int adminUserId, PhpbbGroups group);
        Task LogAdminRankAction(AdminRankActions action, int adminUserId, PhpbbRanks rank);
        Task LogAdminUserAction(AdminUserActions action, int adminUserId, PhpbbUsers user, string? additionalData = null);
        Task LogModeratorPostAction(ModeratorPostActions action, int modUserId, int postId, string? additionalData = null, ITransactionalSqlExecuter? transaction = null);
        Task LogModeratorPostAction(ModeratorPostActions action, int modUserId, PhpbbPosts post, string? additionalData = null, ITransactionalSqlExecuter? transaction = null);
        Task LogModeratorTopicAction(ModeratorTopicActions action, int modUserId, int topicId, string? additionalData = null, ITransactionalSqlExecuter? transaction = null);
        Task LogUserProfileAction(UserProfileActions action, int editingUser, PhpbbUsers targetUser, string? additionalData = null);
    }
}