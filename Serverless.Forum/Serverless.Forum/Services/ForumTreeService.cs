using Dapper;
using Microsoft.EntityFrameworkCore;
using Serverless.Forum.Contracts;
using Serverless.Forum.ForumDb;
using Serverless.Forum.Utilities;
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
        private IEnumerable<int> _restrictedForums = null;

        public ForumTreeService(ForumDbContext context)
        {
            _context = context;
            DefaultTypeMap.MatchNamesWithUnderscores = true;
        }

        public async Task<IEnumerable<int>> GetRestrictedForumList(LoggedUser user)
        {
            if (_restrictedForums != null)
            {
                var allowedForums = (await GetForumTree(usr: user, excludePasswordProtected: true, fullTraversal: true)).Select(f => f.ForumId);
                using var connection = _context.Database.GetDbConnection();
                await connection.OpenIfNeeded();
                _restrictedForums = await connection.QueryAsync<int>("SELECT forum_id FROM phpbb_forums WHERE forum_id NOT IN @allowedForums", new { allowedForums });
            }
            return _restrictedForums;
        }

        public async Task<HashSet<ForumTree>> GetForumTree(int? forumId = null, LoggedUser usr = null, bool excludePasswordProtected = false, bool fullTraversal = false)
        {
            if (_tree != null)
            {
                return _tree;
            }

            var passwordProtected = Enumerable.Empty<int>();
            using (var connection = _context.Database.GetDbConnection())
            {
                await connection.OpenIfNeeded();
                if (excludePasswordProtected)
                {
                    passwordProtected = await connection.QueryAsync<int>("SELECT forum_id FROM phpbb_forums WHERE COALESCE(forum_password, '') <> ''");
                }
                var restrictedForums = (usr?.AllPermissions?.Where(p => p.AuthRoleId == Constants.ACCESS_TO_FORUM_DENIED_ROLE)?.Select(p => p.ForumId) ?? Enumerable.Empty<int>()).Union(passwordProtected);
                _tree = new HashSet<ForumTree>(await connection.QueryAsync<ForumTree>(
                    "CALL `forum`.`get_forum_tree`(@restricted, @forumId, @fullTraversal);",
                    new { restricted = string.Join(',', restrictedForums), forumId, fullTraversal })
                );
            }

            return _tree;
        }

        public async Task<(HashSet<ForumTree> tree, HashSet<PhpbbForums> forums, HashSet<PhpbbTopics> topics, HashSet<Tracking> tracking)> GetExtendedForumTree(int? forumId = null, LoggedUser usr = null, bool forceRefresh = false, bool fullTraversal = false)
        {
            if (new object[] { _tree, _forumData, _topicData, _tracking }.Any(x => x == null) || forceRefresh)
            {
                await GetForumTree(forumId: forumId, usr: usr, fullTraversal: fullTraversal);
                var userId = usr?.UserId; todo:  panoul administratorului trimite null aici
                using var connection = _context.Database.GetDbConnection();
                await connection.OpenIfNeeded();
                using var multi = await connection.QueryMultipleAsync(
                    "SELECT * FROM phpbb_forums WHERE forum_id IN @forumIds; " +
                    "SELECT * FROM phpbb_topics WHERE topic_id IN @topicIds; " +
                    "CALL `forum`.`get_post_tracking`(@userId, @topicId, @forumId);",
                    new
                    {
                        forumIds = _tree.SelectMany(x => x.ChildrenList ?? new HashSet<int>()).Union(_tree.Select(x => x.ForumId)).Distinct(),
                        topicIds = _tree.SelectMany(x => x.TopicsList ?? new HashSet<int>()).Distinct(),
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

        //public async Task<List<ForumTree>> GetPathInTree(ForumTree root, int forumId)
        //{
        //    var track = new List<ForumTree>();
        //    await Traverse(track, root, false, 0, x => x, (x, _) => { }, forumId, -1);
        //    return track;
        //}

        //public async Task<List<T>> GetPathInTree<T>(ForumTree root, Func<ForumTree, T> mapToType, int forumId, int topicId)
        //{
        //    var track = new List<T>();
        //    await Traverse(track, root, false, 0, mapToType, (x, _) => { }, forumId, topicId);
        //    return track;
        //}

        //public async Task<List<T>> GetPathInTree<T>(ForumTree root, Func<ForumTree, T> mapToType)
        //{
        //    var track = new List<T>();
        //    await Traverse(track, root, true, 0, mapToType, (x, _) => { }, -1, -1);
        //    return track;
        //}

        //public async Task<List<T>> GetPathInTree<T>(ForumTree root, Func<ForumTree, T> mapToType, Action<T, int> transformForLevel) where T : class
        //{
        //    var track = new List<T>();
        //    await Traverse(track, root, true, 0, mapToType, transformForLevel, -1, -1);
        //    return track;
        //}

        //private async Task<bool> Traverse<T>(List<T> track, ForumTree node, bool isFullTraversal, int level, Func<ForumTree, T> mapToType, Action<T, int> transformForLevel, int forumId, int topicId) 
        //{
        //    if (node == null)
        //    {
        //        return false;
        //    }

        //    T item = default;

        //    if (!isFullTraversal && ((node.TopicsList?.Any(t => t == topicId) ?? false) || node.ForumId == forumId))
        //    {
        //        item = mapToType(node);
        //        transformForLevel(item, level);
        //        track.Add(item);
        //        return true;
        //    }

        //    item = mapToType(node);
        //    transformForLevel(item, level);
        //    track.Add(item);

        //    foreach (var childId in node.ChildrenList)
        //    {
        //        var child = (await GetForumTree()).FirstOrDefault(t => t.ForumId == childId);
        //        if (await Traverse(track, child, isFullTraversal, level + 1, mapToType, transformForLevel, forumId, topicId))
        //        {
        //            return true;
        //        }
        //    }

        //    if (!isFullTraversal)
        //    {
        //        track.RemoveAt(track.Count - 1);
        //    }
        //    return false;
        //}
    }
}
