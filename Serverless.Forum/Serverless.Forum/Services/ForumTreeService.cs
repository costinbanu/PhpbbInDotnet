using Dapper;
using Microsoft.EntityFrameworkCore;
using Serverless.Forum.Contracts;
using Serverless.Forum.ForumDb;
using Serverless.Forum.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;

namespace Serverless.Forum.Services
{
    public class ForumTreeService
    {
        private readonly ForumDbContext _context;
        private HashSet<ForumTree> _tree = null;
        private HashSet<Tracking> _tracking = null;
        private IEnumerable<(int forumId, bool hasPassword)> _restrictedForums = null;

        public ForumTreeService(ForumDbContext context)
        {
            _context = context;
            DefaultTypeMap.MatchNamesWithUnderscores = true;
        }

        public async Task<IEnumerable<(int forumId, bool hasPassword)>> GetRestrictedForumList(LoggedUser user)
        {
            if (_restrictedForums == null)
            {
                _restrictedForums = (await GetForumTree(user, false)).Where(t => t.IsRestricted).Select(t => (t.ForumId, t.HasPassword));
            }
            return _restrictedForums;
        }

        public async Task<HashSet<ForumTree>> GetForumTree(LoggedUser user, bool forceRefresh)
        {
            if (_tree != null && !forceRefresh)
            {
                return _tree;
            }

            var restrictedForums = (user?.AllPermissions?.Where(p => p.AuthRoleId == Constants.ACCESS_TO_FORUM_DENIED_ROLE)?.Select(p => p.ForumId) ?? Enumerable.Empty<int>()).ToHashSet();
            var tracking = await GetForumTracking(user, forceRefresh);
            using (var connection = _context.Database.GetDbConnection())
            {
                await connection.OpenIfNeeded();
                _tree = (await connection.QueryAsync<ForumTree>("CALL `forum`.`get_forum_tree`();")).ToHashSet();
            }

            void dfs(int forumId)
            {
                var node = GetTreeNode(_tree, forumId);
                if (node == null)
                {
                    return;
                }

                node.IsUnread = tracking.Contains(new Tracking { ForumId = forumId }, new TrackingComparerByForumId());
                node.IsRestricted = restrictedForums.Contains(forumId);
                foreach (var childForumId in node.ChildrenList ?? new HashSet<int>())
                {
                    var childForum = GetTreeNode(_tree, childForumId);
                    if(childForum != null)
                    {
                        childForum.IsRestricted = node.IsRestricted;
                        childForum.HasPassword |= node.HasPassword;
                        if (childForum.PathList == null)
                        {
                            childForum.PathList = new List<int>(node.PathList ?? new List<int>());
                        }
                        childForum.PathList.Add(childForumId);
                        childForum.Level = node.Level + 1;

                        dfs(childForumId);

                        node.IsUnread |= childForum.IsUnread || tracking.Contains(new Tracking { ForumId = childForumId }, new TrackingComparerByForumId());
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

            dfs(0);

            return _tree;
        }

        public async Task<HashSet<Tracking>> GetForumTracking(LoggedUser user, bool forceRefresh)
        {
            if (_tracking != null && !forceRefresh)
            {
                return _tracking;
            }

            using var connection = _context.Database.GetDbConnection();
            await connection.OpenIfNeeded();
            _tracking = new HashSet<Tracking>(await connection.QueryAsync<Tracking>("CALL `forum`.`get_post_tracking`(@userId, null, null);", new { userId = user?.UserId ?? Constants.ANONYMOUS_USER_ID }));
            return _tracking;
        }

        public async Task<bool> IsForumUnread(int forumId, LoggedUser user, bool forceRefresh = false)
            => GetTreeNode(await GetForumTree(user, forceRefresh), forumId)?.IsUnread ?? false;

        public async Task<bool> IsTopicUnread(int topicId, LoggedUser user, bool forceRefresh = false)
        {
            return (await GetForumTracking(user, forceRefresh)).Contains(new Tracking { TopicId = topicId });
        }

        public async Task<bool> IsPostUnread(int topicId, int postId, LoggedUser user)
        {
            var found = (await GetForumTracking(user, false)).TryGetValue(new Tracking { TopicId = topicId }, out var item);
            if (!found)
            {
                return false;
            }
            return new HashSet<int>(new[] { postId }).IsSubsetOf(item.Posts);
        }

        public ForumTree GetTreeNode(HashSet<ForumTree> tree, int forumId)
            => tree.TryGetValue(new ForumTree { ForumId = forumId }, out var node) ? node : null;

        public string GetPathText(HashSet<ForumTree> tree, int forumId)
        {
            var pathParts = GetTreeNode(tree, forumId).PathList ?? new List<int>();
            var sb = new StringBuilder();
            for (var i = 0; i < pathParts.Count; i++)
            {
                var node = GetTreeNode(tree, pathParts[i]);
                if (node?.IsRestricted ?? false)
                {
                    continue;
                }
                sb = sb.Append(HttpUtility.HtmlDecode(node?.ForumName ?? Constants.FORUM_NAME));
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

            return node.ChildrenList?.Select(x => GetTreeNode(tree, x))?.Any(x => !x.IsRestricted) ?? false;
        }
    }
}
