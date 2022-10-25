using Microsoft.Extensions.Configuration;
using PhpbbInDotnet.Database;
using PhpbbInDotnet.Domain;
using PhpbbInDotnet.Domain.Extensions;
using PhpbbInDotnet.Domain.Utilities;
using PhpbbInDotnet.Objects;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;

namespace PhpbbInDotnet.Services
{
    class ForumTreeService : IForumTreeService
    {
        private readonly IForumDbContext _context;
        private readonly IConfiguration _config;
        private readonly ILogger _logger;
        private HashSet<ForumTree>? _tree;
        private HashSet<ForumTopicCount>? _forumTopicCount;
        private Dictionary<int, HashSet<Tracking>>? _tracking;
        private IEnumerable<(int forumId, bool hasPassword)>? _restrictedForums;

        public ForumTreeService(IForumDbContext context, IConfiguration config, ILogger logger)
        {
            _context = context;
            _config = config;
            _logger = logger;
        }

        public async Task<IEnumerable<(int forumId, bool hasPassword)>> GetRestrictedForumList(AuthenticatedUserExpanded user, bool includePasswordProtected = false)
        {
            if (_restrictedForums == null)
            {
                _restrictedForums = (await GetForumTree(user, false, false)).Where(t => IsNodeRestricted(t, includePasswordProtected)).Select(t => (t.ForumId, t.HasPassword));
            }
            return _restrictedForums;
        }

        public async Task<IEnumerable<int>> GetUnrestrictedForums(AuthenticatedUserExpanded user, int? forumId, bool excludePasswordProtected)
        {
            var tree = await GetForumTree(user, false, false);
            var toReturn = new List<int>(tree.Count);

            if (forumId > 0)
            {
                traverse(forumId.Value);
            }
            else
            {
                toReturn.AddRange(tree.Where(t => !IsNodeRestricted(t, excludePasswordProtected)).Select(t => t.ForumId));
            }

            return toReturn.DefaultIfEmpty();

            void traverse(int fid)
            {
                var node = GetTreeNode(tree, fid);
                if (node != null)
                {
                    if (!IsNodeRestricted(node, excludePasswordProtected))
                    {
                        toReturn.Add(fid);
                    }
                    foreach (var child in node?.ChildrenList ?? new HashSet<int>())
                    {
                        traverse(child);
                    }
                }
            }
        }

        static bool IsNodeRestricted(ForumTree tree, bool includePasswordProtected = false)
            => tree.IsRestricted || (includePasswordProtected && tree.HasPassword);

        public async Task<bool> IsForumReadOnlyForUser(AuthenticatedUserExpanded user, int forumId)
        {
            var tree = await GetForumTree(user, false, false);
            var path = new List<int>();
            if (tree.TryGetValue(new ForumTree { ForumId = forumId }, out var cur))
            {
                path = cur?.PathList ?? new List<int>();
            }

            return path.Any(fid => user.IsForumReadOnly(fid));
        }

        public async Task<HashSet<ForumTree>> GetForumTree(AuthenticatedUserExpanded? user, bool forceRefresh, bool fetchUnreadData)
        {
            if (_tree != null && !forceRefresh && !(_tracking == null && fetchUnreadData))
            {
                return _tree;
            }

            var trackingTask = fetchUnreadData ? GetForumTracking(user?.UserId ?? Constants.ANONYMOUS_USER_ID, forceRefresh) : Task.FromResult(new Dictionary<int, HashSet<Tracking>>());
            var treeTask = GetForumTree();
            var forumTopicCountTask = GetForumTopicCount();
            var shortcutParentsTask = fetchUnreadData ? GetShortcutParentForums() : Task.FromResult(new Dictionary<int, List<(int ActualForumId, int TopicId)>>());

            await Task.WhenAll(trackingTask, treeTask, forumTopicCountTask, shortcutParentsTask);

            var tracking = await trackingTask;
            _tree = await treeTask;
            _forumTopicCount = await forumTopicCountTask;
            var shortcutParents = await shortcutParentsTask;

            traverse(0);

            return _tree;

            void traverse(int forumId)
            {
                var node = GetTreeNode(_tree, forumId);
                if (node == null)
                {
                    return;
                }

                node.IsUnread = IsUnread(forumId);
                node.IsRestricted = user?.IsForumRestricted(forumId) == true;
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
                        node.IsUnread |= childForum.IsUnread || IsUnread(childForumId);
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

            async Task<HashSet<ForumTree>> GetForumTree()
            {
                var tree = await _context.GetSqlExecuter().QueryAsync<ForumTree>(
                    "CALL get_forum_tree()");
                return tree.ToHashSet();
            }

            async Task<HashSet<ForumTopicCount>> GetForumTopicCount()
            {
                var count = await _context.GetSqlExecuter().QueryAsync<ForumTopicCount>(
                    "SELECT forum_id, count(topic_id) as topic_count FROM phpbb_topics GROUP BY forum_id");
                return count.ToHashSet();
            }

            async Task<Dictionary<int, List<(int ActualForumId, int TopicId)>>> GetShortcutParentForums()
            {
                var rawData = await _context.GetSqlExecuter().QueryAsync(
                    @"SELECT s.forum_id AS shortcut_forum_id, 
                             s.topic_id,
                             t.forum_id AS actual_forum_id
                        FROM phpbb_shortcuts s
                        JOIN phpbb_topics t on s.topic_id = t.topic_id");

                var toReturn = new Dictionary<int, List<(int, int)>>(rawData.Count());
                foreach (var item in rawData)
                {
                    var shortcutForumId = (int)item.shortcut_forum_id;
                    var actualForumId = (int)item.actual_forum_id;
                    var topicId = (int)item.topic_id;

                    if (!toReturn.ContainsKey(shortcutForumId))
                    {
                        toReturn.Add(shortcutForumId, new List<(int, int)>());
                    }
                    toReturn[shortcutForumId].Add((actualForumId, topicId));
                }

                return toReturn;
            }

            bool IsUnread(int forumId)
            {
                if (!fetchUnreadData)
                {
                    return false;
                }

                return tracking.ContainsKey(forumId)
                    || (shortcutParents.TryGetValue(forumId, out var shortcuts)
                        && shortcuts.Any(shortcut => tracking.TryGetValue(shortcut.ActualForumId, out var track) && track.Contains(new Tracking { TopicId = shortcut.TopicId })));
            }
        }

        public async Task<Dictionary<int, HashSet<Tracking>>> GetForumTracking(int userId, bool forceRefresh)
        {
            if (_tracking != null && !forceRefresh)
            {
                return _tracking;
            }

            var dbResults = Enumerable.Empty<ExtendedTracking>();
            var sqlExecuter = _context.GetSqlExecuter();
            try
            {
                dbResults = await sqlExecuter.QueryAsync<ExtendedTracking>("CALL `forum`.`get_post_tracking`(@userId);", new { userId });
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error getting the forum tracking for user {id}", userId);
            }

            var count = dbResults.Count();
            _tracking = new Dictionary<int, HashSet<Tracking>>(count);

            foreach (var result in dbResults)
            {
                var track = new Tracking
                {
                    Posts = StringUtility.ToIntHashSet(result.PostIds),
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

        public async Task<List<TopicGroup>> GetTopicGroups(int forumId)
        {
            var topics = await _context.GetSqlExecuter().QueryAsync<TopicDto>(
                @"SELECT t.topic_id, 
		                 t.forum_id,
		                 t.topic_title, 
		                 count(p.post_id) AS post_count,
		                 t.topic_views AS view_count,
		                 t.topic_type,
		                 t.topic_last_poster_id,
		                 t.topic_last_poster_name,
		                 t.topic_last_post_time,
		                 t.topic_last_poster_colour,
		                 t.topic_last_post_id,
		                 t.topic_status
	                FROM forum.phpbb_topics t
	                JOIN forum.phpbb_posts p ON t.topic_id = p.topic_id
                   WHERE t.forum_id = @forumId OR topic_type = @global
                   GROUP BY t.topic_id

                  UNION ALL

                  SELECT t.topic_id, 
		                 t.forum_id,
		                 t.topic_title, 
		                 count(p.post_id) AS post_count,
		                 t.topic_views AS view_count,
		                 t.topic_type,
		                 t.topic_last_poster_id,
		                 t.topic_last_poster_name,
		                 t.topic_last_post_time,
		                 t.topic_last_poster_colour,
		                 t.topic_last_post_id,
		                 t.topic_status
	                FROM forum.phpbb_topics t
	                JOIN forum.phpbb_shortcuts s ON t.topic_id = s.topic_id
                    JOIN forum.phpbb_posts p ON t.topic_id = p.topic_id
                   WHERE s.forum_id = @forumId
                   GROUP BY t.topic_id
                           
                   ORDER BY topic_last_post_time DESC",
                new { forumId, global = TopicType.Global });

            return (from t in topics
                    group t by t.TopicType into groups
                    orderby groups.Key descending
                    select new TopicGroup
                    {
                        TopicType = groups.Key,
                        Topics = groups
                    }).ToList();
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

        private IEnumerable<(int ForumId, string ForumName)> GetBreadCrumbs(HashSet<ForumTree> tree, int forumId)
        {
            var pathParts = GetTreeNode(tree, forumId)?.PathList ?? new List<int>();
            for (var i = 0; i < pathParts.Count; i++)
            {
                var node = GetTreeNode(tree, pathParts[i]);
                if (node?.IsRestricted == true)
                {
                    continue;
                }
                yield return (node?.ForumId ?? 0, HttpUtility.HtmlDecode(node?.ForumName ?? _config.GetValue<string>("ForumName")));
            }
        }

        public string GetAbsoluteUrlToForum(int forumId)
            => ForumLinkUtility.GetAbsoluteUrlToForum(_config.GetValue<string>("BaseUrl"), forumId);

        public string GetAbsoluteUrlToTopic(int topicId, int pageNum)
            => ForumLinkUtility.GetAbsoluteUrlToTopic(_config.GetValue<string>("BaseUrl"), topicId, pageNum);

        public BreadCrumbs GetBreadCrumbs(HashSet<ForumTree> tree, int forumId, int? topicId = null, string? topicName = null, int? pageNum = null)
        {
            var elements = new List<ListItemJSLD>
            {
                new ListItemJSLD
                {
                    Position = 1,
                    Name = _config.GetValue<string>("ForumName"),
                    Item = _config.GetValue<string>("BaseUrl")
                }
            };

            var sb = new StringBuilder();
            var isFirst = true;
            foreach (var breadCrumb in GetBreadCrumbs(tree, forumId).Indexed(startIndex: 2))
            {
                if (!isFirst)
                {
                    sb = sb.Append(" → ");
                }
                isFirst = false;
                sb = sb.Append($"<a href=\"{ForumLinkUtility.GetRelativeUrlToForum(breadCrumb.Item.ForumId)}\">{breadCrumb.Item.ForumName}</a>");

                elements.Add(new ListItemJSLD
                {
                    Position = breadCrumb.Index,
                    Name = breadCrumb.Item.ForumName,
                    Item = GetAbsoluteUrlToForum(breadCrumb.Item.ForumId)
                });
            }

            if (topicId is not null && topicName is not null && pageNum is not null)
            {
                var position = elements.Last().Position + 1;
                elements.Add(new ListItemJSLD 
                { 
                    Position = position, 
                    Name = topicName,
                    Item = GetAbsoluteUrlToTopic(topicId.Value, pageNum.Value)
                });
            }

            return new BreadCrumbs
            {
                RawBreadCrumbs = new BreadCrumbJSLD
                {
                    ItemListElement = elements
                },
                ForumPathText = sb.ToString()
            };
        }

        public string GetPathText(HashSet<ForumTree> tree, int forumId)
        {
            var sb = new StringBuilder();
            var isFirst = true;
            foreach (var (_, ForumName) in GetBreadCrumbs(tree, forumId))
            {
                if (!isFirst)
                {
                    sb = sb.Append(" → ");
                }
                isFirst = false;
                sb = sb.Append(ForumName);
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

        public async Task<int> GetFirstUnreadPost(int forumId, int topicId, AuthenticatedUserExpanded user)
        {
            if (user.IsAnonymous)
            {
                return 0;
            }
            Tracking? item = null;
            var found = (await GetForumTracking(user.UserId, false)).TryGetValue(forumId, out var tt) && tt.TryGetValue(new Tracking { TopicId = topicId }, out item);
            if (!found)
            {
                return 0;
            }

            return unchecked((int)((await _context.GetSqlExecuter().QuerySingleOrDefaultAsync(
                "SELECT post_id, post_time FROM phpbb_posts WHERE post_id IN @postIds HAVING post_time = MIN(post_time)",
                new { postIds = item?.Posts.DefaultIfNullOrEmpty() }
            ))?.post_id ?? 0u));
        }

        private int GetTopicCount(int forumId)
            => _forumTopicCount is not null && _forumTopicCount.TryGetValue(new ForumTopicCount { ForumId = forumId }, out var val) ? (val?.TopicCount ?? 0) : 0;
    }
}
