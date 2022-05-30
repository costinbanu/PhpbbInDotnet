using PhpbbInDotnet.Database.Entities;
using PhpbbInDotnet.Objects;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PhpbbInDotnet.Services
{
    public interface IPostService
    {
        Task<(Guid CorrelationId, Dictionary<int, List<AttachmentDto>> Attachments)> CacheAttachmentsAndPrepareForDisplay(List<PhpbbAttachments> dbAttachments, string language, int postCount, bool isPreview);
        Task CascadePostAdd(PhpbbPosts added, bool ignoreTopic);
        Task CascadePostDelete(PhpbbPosts deleted, bool ignoreTopic, bool ignoreAttachmentsAndReports);
        Task CascadePostEdit(PhpbbPosts added);
        Task<PollDto?> GetPoll(PhpbbTopics _currentTopic);
        Task<IEnumerable<PostDto>> GetPosts(int topicId, int pageNum, int pageSize, bool descendingOrder);
    }
}