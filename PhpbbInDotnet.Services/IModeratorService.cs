using PhpbbInDotnet.Objects;
using PhpbbInDotnet.Domain;
using System.Collections.Generic;
using System.Threading.Tasks;
using PhpbbInDotnet.Database.Entities;
using System.Data;

namespace PhpbbInDotnet.Services
{
    public interface IModeratorService
    {
        Task<(string Message, bool? IsSuccess)> ChangeTopicType(int topicId, TopicType topicType, OperationLogDto logDto);
        Task<(string Message, bool? IsSuccess)> CreateShortcut(int topicId, int forumId, OperationLogDto logDto);
        Task<(string Message, bool? IsSuccess)> DeletePosts(int[] postIds, OperationLogDto logDto);
        Task<(string Message, bool? IsSuccess)> DeleteTopic(int topicId, OperationLogDto logDto);
        Task<(string Message, bool? IsSuccess)> DuplicatePost(int postId, OperationLogDto logDto);
        Task<List<ReportDto>> GetReportedMessages(int forumId);
        Task<(string Message, bool? IsSuccess)> LockUnlockTopic(int topicId, bool @lock, OperationLogDto logDto);
        Task<(string Message, bool? IsSuccess)> MovePosts(int[] postIds, int? destinationTopicId, OperationLogDto logDto);
        Task<(string Message, bool? IsSuccess)> MoveTopic(int topicId, int destinationForumId, OperationLogDto logDto);
        Task<(string Message, bool? IsSuccess)> RemoveShortcut(int topicId, int forumId, OperationLogDto logDto);
        Task<(string Message, bool? IsSuccess)> SplitPosts(int[] postIds, int? destinationForumId, OperationLogDto logDto);
        Task CascadePostAdd(PhpbbPosts added, bool ignoreTopic);
        Task CascadePostDelete(PhpbbPosts deleted, bool ignoreTopic, bool ignoreAttachmentsAndReports);
        Task CascadePostEdit(PhpbbPosts edited);

    }
}