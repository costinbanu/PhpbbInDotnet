using Dapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using PhpbbInDotnet.DTOs;
using PhpbbInDotnet.Database;
using PhpbbInDotnet.Utilities;
using System;
using System.Collections.Generic;
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
        private HashSet<ForumTree> _tree = null;
        private Dictionary<int, HashSet<Tracking>> _tracking = null;
        private IEnumerable<(int forumId, bool hasPassword)> _restrictedForums = null;

        public ForumTreeService(ForumDbContext context, IConfiguration config, CommonUtils utils)
        {
            _context = context;
            _config = config;
            _utils = utils;
        }

        public async Task<IEnumerable<(int forumId, bool hasPassword)>> GetRestrictedForumList(LoggedUser user, bool includePasswordProtected = false)
        {
            if (_restrictedForums == null)
            {
                _restrictedForums = (await GetForumTree(user, false)).Where(t => t.IsRestricted || (includePasswordProtected && t.HasPassword)).Select(t => (t.ForumId, t.HasPassword));
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
            var connection = _context.Database.GetDbConnection();
            await connection.OpenIfNeededAsync();
            _tree = (await connection.QueryAsync<ForumTree>("CALL `forum`.`get_forum_tree`();")).ToHashSet();

            void dfs(int forumId)
            {
                var node = GetTreeNode(_tree, forumId);
                if (node == null)
                {
                    return;
                }

                node.IsUnread = tracking.ContainsKey(forumId);
                node.IsRestricted = restrictedForums.Contains(forumId);
                foreach (var childForumId in node.ChildrenList ?? new HashSet<int>())
                {
                    var childForum = GetTreeNode(_tree, childForumId);
                    if(childForum != null)
                    {
                        childForum.IsRestricted = node.IsRestricted;
                        if (childForum.PathList == null)
                        {
                            childForum.PathList = new List<int>(node.PathList ?? new List<int>());
                        }
                        childForum.PathList.Add(childForumId);
                        childForum.Level = node.Level + 1;

                        dfs(childForumId);

                        node.IsUnread |= childForum.IsUnread || tracking.ContainsKey(childForumId);
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

        public async Task<Dictionary<int, HashSet<Tracking>>> GetForumTracking(LoggedUser user, bool forceRefresh)
        {
            if (_tracking != null && !forceRefresh)
            {
                return _tracking;
            }

            var dbResults = Enumerable.Empty<ExtendedTracking>();
            var connection = _context.Database.GetDbConnection();
            try
            {
                await connection.OpenIfNeededAsync();
                dbResults = await connection.QueryAsync<ExtendedTracking>("CALL `forum`.`get_post_tracking`(@userId);", new { userId = user?.UserId ?? Constants.ANONYMOUS_USER_ID });
            }
            catch (Exception ex)
            {
                _utils.HandleError(ex, $"Error getting the forum tracking for user {user?.UserId ?? Constants.ANONYMOUS_USER_ID}");
            }
            
            var count = dbResults.Count();
            _tracking = new Dictionary<int, HashSet<Tracking>>(count);
            
            foreach (var result in dbResults)
            {
                var track = new Tracking
                {
                    Posts = result.PostIds.ToIntHashSet(),
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

        public async Task<bool> IsForumUnread(int forumId, LoggedUser user, bool forceRefresh = false)
            => GetTreeNode(await GetForumTree(user, forceRefresh), forumId)?.IsUnread ?? false;

        public async Task<bool> IsTopicUnread(int forumId, int topicId, LoggedUser user, bool forceRefresh = false)
        {
            var ft = await GetForumTracking(user, forceRefresh);
            return ft.TryGetValue(forumId, out var tt) && tt.Contains(new Tracking { TopicId = topicId });
        }

        public async Task<bool> IsPostUnread(int forumId, int topicId, int postId, LoggedUser user)
        {
            var ft = await GetForumTracking(user, false);
            Tracking item = null;
            var found = ft.TryGetValue(forumId, out var tt) && tt.TryGetValue(new Tracking { TopicId = topicId }, out item);
            if (!found)
            {
                return false;
            }
            return item?.Posts?.Contains(postId) ?? false;
        }

        public ForumTree GetTreeNode(HashSet<ForumTree> tree, int forumId)
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

            return node.ChildrenList?.Select(x => GetTreeNode(tree, x))?.Any(x => !x.IsRestricted) ?? false;
        }
    }
}
