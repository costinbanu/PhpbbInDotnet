using Dapper;
using Microsoft.EntityFrameworkCore;
using Serverless.Forum.Contracts;
using Serverless.Forum.ForumDb;
using Serverless.Forum.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Serverless.Forum.Services
{


    //todo: use dtos from other branch. make everything recursive here. use this sql:

    with child_forums AS(
        SELECT parent.forum_id,
               group_concat(child.forum_id) AS child_list
          FROM phpbb_forums parent

          LEFT JOIN phpbb_forums child ON parent.forum_id = child.parent_id

         GROUP BY parent.forum_id

    ),
	topics AS(
        SELECT f.forum_id,
               group_concat(t.topic_id) AS topic_list
          FROM phpbb_forums f

          LEFT JOIN phpbb_topics t ON f.forum_id = t.forum_id
          WHERE f.forum_id = @forum_id

          GROUP BY f.forum_id

    )
SELECT f.forum_id,
           f.forum_type,
           f.forum_name,
           f.parent_id,
           f.left_id,
           f.forum_desc,
           f.forum_desc_uid,
           cf.child_list,
           t.topic_list,
           f.forum_last_post_id,
           f.forum_last_poster_id,
           f.forum_last_post_subject,
           f.forum_last_post_time,
           f.forum_last_poster_name,
           f.forum_last_poster_colour
      FROM phpbb_forums f

      LEFT JOIN child_forums cf ON f.forum_id = cf.forum_id

      LEFT JOIN topics t ON f.forum_id = t.forum_id


      UNION ALL


      SELECT 0,
			null,
			null,
			null,
			null,
            null,
            null,
            group_concat(cf.forum_id) as child_list,
			null,
			null,
			null,
			null,
			null,
			null,
			null

       FROM phpbb_forums cf
      WHERE cf.parent_id = 0





















    public class ForumTreeService
    {
        private readonly ForumDbContext _context;
        private HashSet<ForumTree> _tree = null;
        private HashSet<Tracking> _tracking = null;
        private HashSet<PhpbbForums> _forumData = null;
        private HashSet<PhpbbTopics> _topicData = null;
        private IEnumerable<(int forumId, bool hasPassword)> _restrictedForums = null;
        private bool _wasFullTraversal = false;

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

        public async Task<HashSet<ForumTree>> GetForumTree(LoggedUser user, bool forceRefresh, int? forumId = null)
        {
            if (_tree != null /*&& fullTraversal && _wasFullTraversal*/ && !forceRefresh)
            {
                return _tree;
            }

            //if (_tree != null && !fullTraversal && _wasFullTraversal && forumId.HasValue)
            //{
            //    var root = _tree.FirstOrDefault(t => t.ForumId == forumId);
            //    _wasFullTraversal = fullTraversal;
            //    return new HashSet<ForumTree>(_tree.Where(t => t.ForumId != forumId && !(t.PathList?.Contains(forumId.Value) ?? false) && Math.Abs(root.Level - t.Level) > 2));
            //}

            //_wasFullTraversal = fullTraversal;

            using (var connection = _context.Database.GetDbConnection())
            {
                await connection.OpenIfNeeded();
                var restrictedForums = user?.AllPermissions?.Where(p => p.AuthRoleId == Constants.ACCESS_TO_FORUM_DENIED_ROLE)?.Select(p => p.ForumId) ?? Enumerable.Empty<int>();
                _tree = new HashSet<ForumTree>(await connection.QueryAsync<ForumTree>(
                    "CALL `forum`.`get_forum_tree`(@restricted, @forumId);",
                    new { restricted = string.Join(',', restrictedForums), forumId })
                );
            }

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

        //public async Task<(HashSet<ForumTree> tree, HashSet<PhpbbForums> forums, HashSet<PhpbbTopics> topics, HashSet<Tracking> tracking)> GetExtendedForumTree(int? forumId = null, LoggedUser usr = null, bool forceRefresh = false, bool fullTraversal = false, bool excludePasswordProtected = false)
        //{
        //    if (new object[] { _tree, _forumData, _topicData }.Any(x => x == null) || forceRefresh || (fullTraversal && !_wasFullTraversal) || (_tracking == null && (usr?.UserId ?? Constants.ANONYMOUS_USER_ID) != Constants.ANONYMOUS_USER_ID))
        //    {
        //        var tree = await GetForumTree(forumId: forumId, usr: usr, fullTraversal: fullTraversal);
        //        tree.RemoveWhere(t => t.Restricted && (!excludePasswordProtected || t.PasswordProtected));
        //        var userId = usr?.UserId;
        //        using var connection = _context.Database.GetDbConnection();
        //        await connection.OpenIfNeeded();
        //        using var multi = await connection.QueryMultipleAsync(
        //            "SELECT * FROM phpbb_forums WHERE forum_id IN @forumIds; " +
        //            "SELECT * FROM phpbb_topics WHERE topic_id IN @topicIds; " +
        //            "CALL `forum`.`get_post_tracking`(@userId, @topicId, @forumId);",
        //            new
        //            {
        //                forumIds = tree.SelectMany(x => x.ChildrenList ?? new HashSet<int>()).Union(tree.Select(x => x.ForumId)).Distinct(),
        //                topicIds = tree.SelectMany(x => x.TopicsList ?? new HashSet<int>()).Distinct(),
        //                userId,
        //                topicId = null as int?,
        //                forumId = null as int?
        //            });
        //        _forumData = new HashSet<PhpbbForums>(await multi.ReadAsync<PhpbbForums>());
        //        _topicData = new HashSet<PhpbbTopics>(await multi.ReadAsync<PhpbbTopics>());
        //        _tracking = new HashSet<Tracking>(await multi.ReadAsync<Tracking>());
        //    }
        //    return (_tree, _forumData, _topicData, _tracking);
        //}

        //public async Task<PhpbbForums> GetForumWithCompleteSummary(PhpbbForums root)
        //{
        //    var (tree, forums, _, _) = await GetExtendedForumTree(fullTraversal: true);
        //    if (!tree.TryGetValue(new ForumTree { ForumId = root.ForumId }, out var treeNode))
        //    {
        //        return root;
        //    }

        //    PhpbbForums maxForumChild = null;
        //    foreach (var child in treeNode.ChildrenList ?? new HashSet<int>())
        //    { 
        //        if (!forums.TryGetValue(new PhpbbForums { ForumId = child }, out var childForum))
        //        {
        //            continue;
        //        }
        //        var curSummary = await GetForumWithCompleteSummary(childForum);
        //        if ((maxForumChild?.ForumLastPostTime ?? 0) < curSummary.ForumLastPostTime)
        //        {
        //            maxForumChild = curSummary;
        //        }
        //    }

        //    if (maxForumChild == null)
        //    {
        //        return root;
        //    }

        //    if (root.ForumLastPostTime < (maxForumChild?.ForumLastPostTime ?? 0))
        //    {
        //        root.ForumLastPosterColour = maxForumChild.ForumLastPosterColour;
        //        root.ForumLastPosterId = maxForumChild.ForumLastPosterId;
        //        root.ForumLastPosterName = maxForumChild.ForumLastPosterName;
        //        root.ForumLastPostId = maxForumChild.ForumLastPostId;
        //        root.ForumLastPostSubject = maxForumChild.ForumLastPostSubject;
        //        root.ForumLastPostTime = maxForumChild.ForumLastPostTime;
        //    }
        //    return root;
        //}

        public async Task<bool> IsForumUnread(int forumId, LoggedUser user, bool forceRefresh = false)
        {
            var tree = await GetForumTree(user, forceRefresh);
            var tracking = await GetForumTracking(user, forceRefresh);
            if (tracking.Contains(new Tracking { ForumId = forumId }, new TrackingComparerByForumId()))
            {
                return true;
            }
            if (!tree.TryGetValue(new ForumTree { ForumId = forumId }, out var curForum))
            {
                return false;
            }
            foreach (var topic in curForum.TopicsList ?? new HashSet<int>())
            {
                if (await IsTopicUnread(topic, user, forceRefresh))
                {
                    return true;
                }
            }
            bool childUnread = false;
            foreach (var child in curForum.ChildrenList ?? new HashSet<int>())
            {
                if (tracking.Contains(new Tracking { ForumId = child }, new TrackingComparerByForumId()))
                {
                    return true;
                }
                childUnread |= await IsForumUnread(child, user, forceRefresh);
            }
            return childUnread;
        }

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
    }
}
