using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using PhpbbInDotnet.Database.SqlExecuter;
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
        private readonly ISqlExecuter _sqlExecuter;
        private readonly IConfiguration _config;
        private readonly ILogger _logger;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private HashSet<ForumTree>? _tree;
        private HashSet<ForumTopicCount>? _forumTopicCount;
        private Dictionary<int, HashSet<Tracking>>? _tracking;
        private IEnumerable<(int forumId, bool hasPassword)>? _restrictedForums;

        public ForumTreeService(ISqlExecuter sqlExecuter, IConfiguration config, ILogger logger, IHttpContextAccessor httpContextAccessor)
        {
            _sqlExecuter = sqlExecuter;
            _config = config;
            _logger = logger;
            _httpContextAccessor = httpContextAccessor;
        }

        public async Task<IEnumerable<(int forumId, bool hasPassword)>> GetRestrictedForumList(ForumUserExpanded user, bool includePasswordProtected = false)
            => _restrictedForums ??= (await GetForumTree(user, false, false)).Where(t => IsNodeRestricted(t, user.UserId, includePasswordProtected)).Select(t => (t.ForumId, t.HasPassword));

        public async Task<IEnumerable<int>> GetUnrestrictedForums(ForumUserExpanded user, int forumId, bool ignoreForumPassword)
        {
            var tree = await GetForumTree(user, false, false);
            var toReturn = new List<int>(tree.Count);
            var reachedNode = false;

            traverse(forumId);

            return toReturn.DefaultIfEmpty();

            void traverse(int fid)
            {
                reachedNode |= forumId == fid;
                var node = GetTreeNode(tree, fid);
                if (node != null)
                {
                    if (!IsNodeRestricted(node, user.UserId, includePasswordProtected: !ignoreForumPassword))
                    {
                        if (reachedNode)
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
        }

        public bool IsNodeRestricted(ForumTree tree, int userId, bool includePasswordProtected)
            => tree.IsRestricted || (includePasswordProtected && tree.HasPassword && _httpContextAccessor.HttpContext?.Request.Cookies.IsUserLoggedIntoForum(userId, tree.ForumId) != true);
        
        public async Task<bool> IsForumReadOnlyForUser(ForumUserExpanded user, int forumId)
        {
            var tree = await GetForumTree(user, false, false);
            var path = new List<int>();
            if (tree.TryGetValue(new ForumTree { ForumId = forumId }, out var cur))
            {
                path = cur?.PathList ?? new List<int>();
            }

            return path.Any(fid => user.IsForumReadOnly(fid));
        }

        public async Task<HashSet<ForumTree>> GetForumTree(ForumUserExpanded? user, bool forceRefresh, bool fetchUnreadData)
        {
            if (_tree != null && !forceRefresh && !(_tracking == null && fetchUnreadData))
            {
                return _tree;
            }

            var tracking = new Dictionary<int, HashSet<Tracking>>();
            var shortcutParents = new Dictionary<int, List<(int ActualForumId, int TopicId)>>();
            if (fetchUnreadData)
            {
                tracking = await GetForumTracking(user?.UserId ?? Constants.ANONYMOUS_USER_ID, forceRefresh);
                shortcutParents = await GetShortcutParentForums();
            }
            _tree = await GetForumTree();
            _forumTopicCount = await GetForumTopicCount();

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
                        //childForum.IsRestricted |= node.IsRestricted;
                        //childForum.HasPassword |= node.HasPassword;
                        childForum.PathList ??= new List<int>(node.PathList ?? new List<int>());
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
                var tree = await _sqlExecuter.CallStoredProcedureAsync<ForumTree>("get_forum_tree");
                return tree.ToHashSet();
            }

            async Task<HashSet<ForumTopicCount>> GetForumTopicCount()
            {
                var count = await _sqlExecuter.QueryAsync<ForumTopicCount>(
                    "SELECT forum_id, count(topic_id) as topic_count FROM phpbb_topics GROUP BY forum_id");
                return count.ToHashSet();
            }

            async Task<Dictionary<int, List<(int ActualForumId, int TopicId)>>> GetShortcutParentForums()
            {
                var rawData = await _sqlExecuter.QueryAsync(
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
            try
            {
                dbResults = await _sqlExecuter.CallStoredProcedureAsync<ExtendedTracking>("get_post_tracking", new { userId });
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
            var topics = await _sqlExecuter.QueryAsync<TopicDto>(
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
	                FROM phpbb_topics t
	                JOIN phpbb_posts p ON t.topic_id = p.topic_id
                   WHERE t.forum_id = @forumId OR topic_type = @global
                   GROUP BY t.topic_id, 
                            t.forum_id,
                            t.topic_title, 
                            t.topic_views,
                            t.topic_type,
                            t.topic_last_poster_id,
                            t.topic_last_poster_name,
                            t.topic_last_post_time,
                            t.topic_last_poster_colour,
                            t.topic_last_post_id,
                            t.topic_status

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
	                FROM phpbb_topics t
	                JOIN phpbb_shortcuts s ON t.topic_id = s.topic_id
                    JOIN phpbb_posts p ON t.topic_id = p.topic_id
                   WHERE s.forum_id = @forumId
                   GROUP BY t.topic_id, 
                            t.forum_id,
                            t.topic_title, 
                            t.topic_views,
                            t.topic_type,
                            t.topic_last_poster_id,
                            t.topic_last_poster_name,
                            t.topic_last_post_time,
                            t.topic_last_poster_colour,
                            t.topic_last_post_id,
                            t.topic_status
                           
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

        public async Task<bool> IsForumUnread(int forumId, ForumUserExpanded user, bool forceRefresh = false)
            => GetTreeNode(await GetForumTree(user, forceRefresh, true), forumId)?.IsUnread ?? false;

        public async Task<bool> IsTopicUnread(int forumId, int topicId, ForumUserExpanded user, bool forceRefresh = false)
        {
            var ft = await GetForumTracking(user?.UserId ?? Constants.ANONYMOUS_USER_ID, forceRefresh);
            return ft.TryGetValue(forumId, out var tt) && tt.Contains(new Tracking { TopicId = topicId });
        }

        public async Task<bool> IsPostUnread(int forumId, int topicId, int postId, ForumUserExpanded user)
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
                    sb = sb.Append(Constants.FORUM_PATH_SEPARATOR);
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
                    sb = sb.Append(Constants.FORUM_PATH_SEPARATOR);
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

        public async Task<int> GetFirstUnreadPost(int forumId, int topicId, ForumUserExpanded user)
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

            return item!.Posts.DefaultIfNullOrEmpty().Min();
        }

        private int GetTopicCount(int forumId)
            => _forumTopicCount is not null && _forumTopicCount.TryGetValue(new ForumTopicCount { ForumId = forumId }, out var val) ? (val?.TopicCount ?? 0) : 0;

        public async Task MarkForumAndSubforumsRead(ForumUserExpanded user, int forumId)
        {
            var node = GetTreeNode(await GetForumTree(user, false, false), forumId);
            if (node == null)
            {
                if (forumId == 0)
                {
                    await SetLastMark(user.UserId);
                }
                return;
            }

            await MarkForumRead(user.UserId, forumId);
            foreach (var child in node.ChildrenList ?? new HashSet<int>())
            {
                await MarkForumAndSubforumsRead(user, child);
            }
        }

        public async Task MarkForumRead(int userId, int forumId)
        {
            try
            {
                await _sqlExecuter.CallStoredProcedureAsync("mark_forum_read", new { forumId, userId, markTime = DateTime.UtcNow.ToUnixTimestamp() });
                await _sqlExecuter.ExecuteAsyncWithoutResiliency(
                    "UPDATE phpbb_forums_watch SET notify_status = 0 WHERE forum_id = @forumId AND user_id = @userId",
                    new { forumId, userId });
            }
            catch (Exception ex)
            {
                _logger.Warning(ex, "Error marking forums as read.");
            }
        }

        public async Task MarkTopicRead(int userId, int forumId, int topicId, bool isLastPage, long markTime)
        {
            var tracking = await GetForumTracking(userId, false);
            if (tracking!.TryGetValue(forumId, out var tt) && tt.Count == 1 && isLastPage)
            {
                //current topic was the last unread in its forum, and it is the last page of unread messages, so mark the whole forum read
                await MarkForumRead(userId, forumId);

                //current forum is the user's last unread forum, and it has just been read; set the mark time.
                if (tracking.Count == 1)
                {
                    await SetLastMark(userId);
                }
            }
            else
            {
                //there are other unread topics in this forum, or unread pages in this topic, so just mark the current page as read
                try
                {
                    await _sqlExecuter.CallStoredProcedureAsync("mark_topic_read", new { forumId, topicId, userId, markTime });
                    await _sqlExecuter.ExecuteAsyncWithoutResiliency(
                        "UPDATE phpbb_topics_watch SET notify_status = 0 WHERE topic_id = @topicId AND user_id = @userId",
                        new { topicId, userId });
                }
                catch (Exception ex)
                {
                    _logger.Warning(ex, "Error marking topics as read (forumId={forumId}, topicId={topicId}, userId={userId}).", forumId, topicId, userId);
                }
            }
        }

        private async Task SetLastMark(int userId)
        {
            try
            {
                await _sqlExecuter.ExecuteAsync(
                    "UPDATE phpbb_users SET user_lastmark = @markTime WHERE user_id = @userId", 
                    new { markTime = DateTime.UtcNow.ToUnixTimestamp(), userId });
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error setting user last mark.");
            }
        }
    }
}
