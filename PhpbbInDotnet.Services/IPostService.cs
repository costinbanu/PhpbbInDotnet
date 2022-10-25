using PhpbbInDotnet.Database.Entities;
using PhpbbInDotnet.Objects;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PhpbbInDotnet.Services
{
    public interface IPostService
    {
        Task<(Guid CorrelationId, Dictionary<int, List<AttachmentDto>> Attachments)> CacheAttachmentsAndPrepareForDisplay(IEnumerable<PhpbbAttachments> dbAttachments, int forumId, string language, int postCount, bool isPreview, int userId);
        Task<(Guid CorrelationId, Dictionary<int, List<AttachmentDto>> Attachments)> CacheAttachmentsAndPrepareForDisplay(IEnumerable<PhpbbAttachmentExpanded> dbAttachments, string language, int postCount, bool isPreview, int userId);
        Task CascadePostAdd(PhpbbPosts added, bool ignoreTopic);
        Task CascadePostDelete(PhpbbPosts deleted, bool ignoreTopic, bool ignoreAttachmentsAndReports);
        Task CascadePostEdit(PhpbbPosts added);
        Task<PollDto?> GetPoll(PhpbbTopics _currentTopic);
        Task<PostListDto> GetPosts(int topicId, int pageNum, int pageSize, bool isPostingView, string language, int userId);
    }
}