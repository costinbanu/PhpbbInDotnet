using PhpbbInDotnet.Objects;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PhpbbInDotnet.Services
{
    public interface IForumTreeService
    {
        Task<int> GetFirstUnreadPost(int forumId, int topicId, ForumUserExpanded user);
        Task<Dictionary<int, HashSet<Tracking>>> GetForumTracking(int userId, bool forceRefresh);
        Task<HashSet<ForumTree>> GetForumTree(ForumUserExpanded? user, bool forceRefresh, bool fetchUnreadData);
        string GetPathText(HashSet<ForumTree> tree, int forumId);
        string GetAbsoluteUrlToForum(int forumId);
        string GetAbsoluteUrlToTopic(int topicId, int pageNum);
        BreadCrumbs GetBreadCrumbs(HashSet<ForumTree> tree, int forumId, int? topicId = null, string? topicName = null, int? pageNum = null);
        Task<IEnumerable<(int forumId, bool hasPassword)>> GetRestrictedForumList(ForumUserExpanded user, bool includePasswordProtected = false);
        Task<IEnumerable<int>> GetUnrestrictedForums(ForumUserExpanded user, int forumId = 0, bool ignoreForumPassword = false);
        bool IsNodeRestricted(ForumTree tree, int userId, bool includePasswordProtected);
        Task<List<TopicGroup>> GetTopicGroups(int forumId);
        ForumTree? GetTreeNode(HashSet<ForumTree> tree, int forumId);
        bool HasUnrestrictedChildren(HashSet<ForumTree> tree, int forumId);
        Task<bool> IsForumReadOnlyForUser(ForumUserExpanded user, int forumId);
        Task<bool> IsForumUnread(int forumId, ForumUserExpanded user);
        Task<bool> IsPostUnread(int forumId, int topicId, int postId, ForumUserExpanded user);
        Task<bool> IsTopicUnread(int forumId, int topicId, ForumUserExpanded user);
        Task MarkForumAndSubforumsRead(ForumUserExpanded user, int forumId);
        Task MarkForumRead(int userId, int forumId);
        Task MarkTopicRead(int userId, int forumId, int topicId, bool isLastPage, long markTime);
    }
}