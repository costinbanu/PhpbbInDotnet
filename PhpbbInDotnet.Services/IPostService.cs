using PhpbbInDotnet.Database.Entities;
using PhpbbInDotnet.Database.SqlExecuter;
using PhpbbInDotnet.Objects;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PhpbbInDotnet.Services
{
    public interface IPostService
    {
        Task<(Guid CorrelationId, Dictionary<int, List<AttachmentDto>> Attachments)> CacheAttachmentsAndPrepareForDisplay(IEnumerable<PhpbbAttachments> dbAttachments, int forumId, string language, int postCount, bool isPreview);
        Task<(Guid CorrelationId, Dictionary<int, List<AttachmentDto>> Attachments)> CacheAttachmentsAndPrepareForDisplay(IEnumerable<PhpbbAttachmentExpanded> dbAttachments, string language, int postCount, bool isPreview);
        Task<PollDto?> GetPoll(PhpbbTopics _currentTopic);
        Task<PostListDto> GetPosts(int topicId, int pageNum, int pageSize, bool isPostingView, string language);
		Task CascadePostAdd(ITransactionalSqlExecuter transaction, bool ignoreUser, bool ignoreForums, IEnumerable<PhpbbPosts> added);
		Task CascadePostAdd(ITransactionalSqlExecuter transaction, bool ignoreUser, bool ignoreForums, params PhpbbPosts[] added);
		Task CascadePostDelete(ITransactionalSqlExecuter transaction, bool ignoreUser, bool ignoreForums, bool ignoreTopics, bool ignoreAttachmentsAndReports, IEnumerable<PhpbbPosts> deleted);
		Task CascadePostEdit(PhpbbPosts edited, ITransactionalSqlExecuter transaction);
		Task SyncForumWithPosts(ITransactionalSqlExecuter transaction, params int[] forumIds);
	}
}