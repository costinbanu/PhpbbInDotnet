using PhpbbInDotnet.Objects;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PhpbbInDotnet.Services
{
    public interface IForumTreeService
    {
        Task<int> GetFirstUnreadPost(int forumId, int topicId, AuthenticatedUserExpanded user);
        Task<Dictionary<int, HashSet<Tracking>>> GetForumTracking(int userId, bool forceRefresh);
        Task<HashSet<ForumTree>> GetForumTree(AuthenticatedUserExpanded? user, bool forceRefresh, bool fetchUnreadData);
        string GetPathText(HashSet<ForumTree> tree, int forumId);
        string GetAbsoluteUrlToForum(int forumId);
        string GetAbsoluteUrlToTopic(int topicId, int pageNum);
        BreadCrumbs GetBreadCrumbs(HashSet<ForumTree> tree, int forumId, int? topicId = null, string? topicName = null, int? pageNum = null);
        Task<IEnumerable<(int forumId, bool hasPassword)>> GetRestrictedForumList(AuthenticatedUserExpanded user, bool includePasswordProtected = false);
        Task<IEnumerable<int>> GetUnrestrictedForums(AuthenticatedUserExpanded user, int? forumId = null, bool excludePasswordProtected = true);
        Task<List<TopicGroup>> GetTopicGroups(int forumId);
        ForumTree? GetTreeNode(HashSet<ForumTree> tree, int forumId);
        bool HasUnrestrictedChildren(HashSet<ForumTree> tree, int forumId);
        Task<bool> IsForumReadOnlyForUser(AuthenticatedUserExpanded user, int forumId);
        Task<bool> IsForumUnread(int forumId, AuthenticatedUserExpanded user, bool forceRefresh = false);
        Task<bool> IsPostUnread(int forumId, int topicId, int postId, AuthenticatedUserExpanded user);
        Task<bool> IsTopicUnread(int forumId, int topicId, AuthenticatedUserExpanded user, bool forceRefresh = false);
    }
}