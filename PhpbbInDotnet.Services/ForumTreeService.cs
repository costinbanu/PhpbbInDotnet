using Dapper;
using Microsoft.Extensions.Configuration;
using PhpbbInDotnet.Database;
using PhpbbInDotnet.Objects;
using PhpbbInDotnet.Utilities;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;

namespace PhpbbInDotnet.Services
{
    public class ForumTreeService
    {
        private readonly ForumDbContext _context;
        private readonly IConfiguration _config;
        private readonly CommonUtils _utils;
        private HashSet<ForumTree>? _tree;
        private HashSet<ForumTopicCount>? _forumTopicCount;
        private Dictionary<int, HashSet<Tracking>>? _tracking;
        private IEnumerable<(int forumId, bool hasPassword)>? _restrictedForums;

        public ForumTreeService(ForumDbContext context, IConfiguration config, CommonUtils utils)
        {
            _context = context;
            _config = config;
            _utils = utils;
        }

        public async Task<IEnumerable<(int forumId, bool hasPassword)>> GetRestrictedForumList(AuthenticatedUserExpanded user, bool includePasswordProtected = false)
        {
            if (_restrictedForums == null)
            {
                _restrictedForums = (await GetForumTree(user, false, false)).Where(t => IsNodeRestricted(t, includePasswordProtected)).Select(t => (t.ForumId, t.HasPassword));
            }
            return _restrictedForums;
        }

        public bool IsNodeRestricted(ForumTree tree, bool includePasswordProtected = false)
            => tree.IsRestricted || (includePasswordProtected && tree.HasPassword);

        public async Task<HashSet<ForumTree>> GetForumTree(AuthenticatedUserExpanded? user, bool forceRefresh, bool fetchUnreadData)
        {
            if (_tree != null && !forceRefresh && !(_tracking == null && fetchUnreadData))
            {
                return _tree;
            }

            var tracking = fetchUnreadData ? await GetForumTracking(user?.UserId ?? Constants.ANONYMOUS_USER_ID, forceRefresh) : null;
            var connection = _context.GetDbConnection();

            var treeTask = GetForumTree(connection);
            var forumTopicCountTask = GetForumTopicCount(connection);
            var restrictedForumsTask = GetRestrictedForumsFromPermissions(user);
            await Task.WhenAll(treeTask, forumTopicCountTask, restrictedForumsTask);
            _tree = await treeTask;
            _forumTopicCount = await forumTopicCountTask;
            var restrictedForums = await restrictedForumsTask;

            traverse(0);

            return _tree;

            void traverse(int forumId)
            {
                var node = GetTreeNode(_tree, forumId);
                if (node == null)
                {
                    return;
                }

                node.IsUnread = fetchUnreadData && tracking!.ContainsKey(forumId);
                node.IsRestricted = restrictedForums.Contains(forumId);
                node.TotalSubforumCount = node.ChildrenList?.Count ?? 0;
                node.TotalTopicCount = GetTopicCount(forumId);
                foreach (var childForumId in node.ChildrenList ?? new HashSet<int>())
                {
                    var childForum = GetTreeNode(_tree, childForumId);
                    if (childForum != null)
                    {
                        childForum.IsRestricted = node.IsRestricted;
                        if (childForum.PathList == null)
                        {
                            childForum.PathList = new List<int>(node.PathList ?? new List<int>());
                        }
                        childForum.PathList.Add(childForumId);
                        childForum.Level = node.Level + 1;

                        traverse(childForumId);

                        node.TotalSubforumCount += childForum.TotalSubforumCount;
                        node.TotalTopicCount += childForum.TotalTopicCount;
                        node.IsUnread |= childForum.IsUnread || (fetchUnreadData && tracking!.ContainsKey(childForumId));
                        if ((node.ForumLastPostTime ?? 0) < (childForum.ForumLastPostTime ?? 0))
                        {
                            node.ForumLastPosterColour = childForum.ForumLastPosterColour;
                            node.ForumLastPosterId = childForum.ForumLastPosterId;
                            node.ForumLastPosterName = childForum.ForumLastPosterName;
                            node.ForumLastPostId = childForum.ForumLastPostId;
                            node.ForumLastPostSubject = childForum.ForumLastPostSubject;
                            node.ForumLastPostTime = childForum.ForumLastPostTime;
                        }
                    }
                }
            }

            async Task<HashSet<ForumTree>> GetForumTree(DbConnection connection)
            {
                var tree = await connection.QueryAsync<ForumTree>("CALL get_forum_tree()");
                return tree.ToHashSet();
            }

            async Task<HashSet<ForumTopicCount>> GetForumTopicCount(DbConnection connection)
            {
                var count = await connection.QueryAsync<ForumTopicCount>("SELECT forum_id, count(topic_id) as topic_count FROM phpbb_topics GROUP BY forum_id");
                return count.ToHashSet();
            }

            Task<HashSet<int>> GetRestrictedForumsFromPermissions(AuthenticatedUserExpanded? user)
                => Task.Run(() => (user?.AllPermissions?.Where(p => p.AuthRoleId == Constants.ACCESS_TO_FORUM_DENIED_ROLE)?.Select(p => p.ForumId) ?? Enumerable.Empty<int>()).ToHashSet());
        }

        public async Task<Dictionary<int, HashSet<Tracking>>> GetForumTracking(int userId, bool forceRefresh)
        {
            if (_tracking != null && !forceRefresh)
            {
                return _tracking;
            }

            var dbResults = Enumerable.Empty<ExtendedTracking>();
            var connection = _context.GetDbConnection();
            try
            {
                dbResults = await connection.QueryAsync<ExtendedTracking>("CALL `forum`.`get_post_tracking`(@userId);", new { userId });
            }
            catch (Exception ex)
            {
                _utils.HandleError(ex, $"Error getting the forum tracking for user {userId}");
            }
            
            var count = dbResults.Count();
            _tracking = new Dictionary<int, HashSet<Tracking>>(count);
            
            foreach (var result in dbResults)
            {
                var track = new Tracking
                {
                    Posts = result.PostIds?.ToIntHashSet(),
                    TopicId = result.TopicId
                };

                if (!_tracking.ContainsKey(result.ForumId))
                {
                    _tracking.Add(result.ForumId, new HashSet<Tracking>(count));
                }

                _tracking[result.ForumId].Add(track);
            }

            return _tracking;
        }

        public async Task<bool> IsForumUnread(int forumId, AuthenticatedUserExpanded user, bool forceRefresh = false)
            => GetTreeNode(await GetForumTree(user, forceRefresh, true), forumId)?.IsUnread ?? false;

        public async Task<bool> IsTopicUnread(int forumId, int topicId, AuthenticatedUserExpanded user, bool forceRefresh = false)
        {
            var ft = await GetForumTracking(user?.UserId ?? Constants.ANONYMOUS_USER_ID, forceRefresh);
            return ft.TryGetValue(forumId, out var tt) && tt.Contains(new Tracking { TopicId = topicId });
        }

        public async Task<bool> IsPostUnread(int forumId, int topicId, int postId, AuthenticatedUserExpanded user)
        {
            var ft = await GetForumTracking(user?.UserId ?? Constants.ANONYMOUS_USER_ID, false);
            Tracking? item = null;
            var found = ft.TryGetValue(forumId, out var tt) && tt.TryGetValue(new Tracking { TopicId = topicId }, out item);
            if (!found)
            {
                return false;
            }
            return item?.Posts?.Contains(postId) ?? false;
        }

        public ForumTree? GetTreeNode(HashSet<ForumTree> tree, int forumId)
            => tree.TryGetValue(new ForumTree { ForumId = forumId }, out var node) ? node : null;

        public string GetPathText(HashSet<ForumTree> tree, int forumId)
        {
            var pathParts = GetTreeNode(tree, forumId)?.PathList ?? new List<int>();
            var sb = new StringBuilder();
            for (var i = 0; i < pathParts.Count; i++)
            {
                var node = GetTreeNode(tree, pathParts[i]);
                if (node?.IsRestricted ?? false)
                {
                    continue;
                }
                sb = sb.Append(HttpUtility.HtmlDecode(node?.ForumName ?? _config.GetValue<string>("ForumName")));
                if (i < pathParts.Count - 1)
                {
                    sb = sb.Append(" → ");
                }
            }
            return sb.ToString();
        }

        public bool HasUnrestrictedChildren(HashSet<ForumTree> tree, int forumId)
        {
            var node = GetTreeNode(tree, forumId);
            if (node == null)
            {
                return false;
            }

            return node.ChildrenList?.Select(x => GetTreeNode(tree, x))?.Any(x => x?.IsRestricted == false) ?? false;
        }

        private int GetTopicCount(int forumId)
            => _forumTopicCount is not null && _forumTopicCount.TryGetValue(new ForumTopicCount { ForumId = forumId }, out var val) ? (val?.TopicCount ?? 0) : 0;
    }
}
