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
                _restrictedForums = (await GetForumTree(usr: user, fullTraversal: true)).Where(t => t.PasswordProtected || t.Restricted).Select(t => (t.ForumId, t.PasswordProtected));
            }
            return _restrictedForums;
        }

        public async Task<HashSet<ForumTree>> GetForumTree(int? forumId = null, LoggedUser usr = null, bool fullTraversal = false)
        {
            if (_tree != null && fullTraversal && _wasFullTraversal)
            {
                return _tree;
            }

            if (_tree != null && !fullTraversal && _wasFullTraversal && forumId.HasValue)
            {
                var root = _tree.FirstOrDefault(t => t.ForumId == forumId);
                _wasFullTraversal = fullTraversal;
                return new HashSet<ForumTree>(_tree.Where(t => t.ForumId != forumId && !(t.PathList?.Contains(forumId.Value) ?? false) && Math.Abs(root.Level - t.Level) > 2));
            }

            _wasFullTraversal = fullTraversal;

            using (var connection = _context.Database.GetDbConnection())
            {
                await connection.OpenIfNeeded();
                var restrictedForums = usr?.AllPermissions?.Where(p => p.AuthRoleId == Constants.ACCESS_TO_FORUM_DENIED_ROLE)?.Select(p => p.ForumId) ?? Enumerable.Empty<int>();
                _tree = new HashSet<ForumTree>(await connection.QueryAsync<ForumTree>(
                    "CALL `forum`.`get_forum_tree`(@restricted, @forumId, @fullTraversal);",
                    new { restricted = string.Join(',', restrictedForums), forumId, fullTraversal })
                );
            }

            return _tree;
        }

        public async Task<(HashSet<ForumTree> tree, HashSet<PhpbbForums> forums, HashSet<PhpbbTopics> topics, HashSet<Tracking> tracking)> GetExtendedForumTree(int? forumId = null, LoggedUser usr = null, bool forceRefresh = false, bool fullTraversal = false, bool excludePasswordProtected = false)
        {
            if (new object[] { _tree, _forumData, _topicData, _tracking }.Any(x => x == null) || forceRefresh || (fullTraversal && !_wasFullTraversal))
            {
                var tree = await GetForumTree(forumId: forumId, usr: usr, fullTraversal: fullTraversal);
                tree.RemoveWhere(t => t.Restricted && (!excludePasswordProtected || t.PasswordProtected));
                var userId = usr?.UserId;
                using var connection = _context.Database.GetDbConnection();
                await connection.OpenIfNeeded();
                using var multi = await connection.QueryMultipleAsync(
                    "SELECT * FROM phpbb_forums WHERE forum_id IN @forumIds; " +
                    "SELECT * FROM phpbb_topics WHERE topic_id IN @topicIds; " +
                    "CALL `forum`.`get_post_tracking`(@userId, @topicId, @forumId);",
                    new
                    {
                        forumIds = tree.SelectMany(x => x.ChildrenList ?? new HashSet<int>()).Union(tree.Select(x => x.ForumId)).Distinct(),
                        topicIds = tree.SelectMany(x => x.TopicsList ?? new HashSet<int>()).Distinct(),
                        userId,
                        topicId = null as int?,
                        forumId
                    });
                _forumData = new HashSet<PhpbbForums>(await multi.ReadAsync<PhpbbForums>());
                _topicData = new HashSet<PhpbbTopics>(await multi.ReadAsync<PhpbbTopics>());
                _tracking = new HashSet<Tracking>(await multi.ReadAsync<Tracking>());
            }
            return (_tree, _forumData, _topicData, _tracking);
        }

        public async Task<PhpbbForums> GetForumWithCompleteSummary(PhpbbForums root)
        {
            if (root.ForumLastPostTime > 0)
            {
                return root;
            }

            var (tree, forums, _, _) = await GetExtendedForumTree();
            if (!tree.TryGetValue(new ForumTree { ForumId = root.ForumId }, out var treeNode))
            {
                return root;
            }

            PhpbbForums maxChild = null;
            foreach (var child in treeNode.ChildrenList)
            { 
                if (!forums.TryGetValue(new PhpbbForums { ForumId = child }, out var childForum))
                {
                    continue;
                }
                if (childForum.ForumLastPostTime == 0)
                {
                    maxChild = await GetForumWithCompleteSummary(childForum);
                }
                if ((maxChild?.ForumLastPostTime ?? 0) < childForum.ForumLastPostTime)
                {
                    maxChild = childForum;
                }
            }
            if (maxChild == null)
            {
                return root;
            }
            root.ForumLastPosterColour = maxChild.ForumLastPosterColour;
            root.ForumLastPosterId = maxChild.ForumLastPosterId;
            root.ForumLastPosterName = maxChild.ForumLastPosterName;
            root.ForumLastPostId = maxChild.ForumLastPostId;
            root.ForumLastPostSubject = maxChild.ForumLastPostSubject;
            root.ForumLastPostTime = maxChild.ForumLastPostTime;
            return root;
        }
    }
}
