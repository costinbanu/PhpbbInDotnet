using Dapper;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.EntityFrameworkCore;
using Serverless.Forum.Contracts;
using Serverless.Forum.ForumDb;
using Serverless.Forum.Pages;
using Serverless.Forum.Utilities;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;

namespace Serverless.Forum.Services
{
    public class ForumTreeService
    {
        private readonly ForumDbContext _context;
        private HashSet<ForumTree> _tree;

        public ForumTreeService(ForumDbContext context)
        {
            _context = context;
            DefaultTypeMap.MatchNamesWithUnderscores = true;
        }

        public async Task<IEnumerable<int>> GetRestrictedForumList(LoggedUser user)
        {
            var allowedForums = new List<int>();
            var root = (await GetForumTree(usr: user, excludePasswordProtected: true)).FirstOrDefault(f => f.ForumId == 0);
            await Traverse(allowedForums, root, true, 0, f => f.ForumId, (_, __) => { }, -1, -1);
            using var connection = _context.Database.GetDbConnection();
            await connection.OpenIfNeeded();
            return await connection.QueryAsync<int>("SELECT forum_id FROM phpbb_forums WHERE forum_id NOT IN @allowedForums", new { allowedForums });
        }

        public async Task<HashSet<ForumTree>> GetForumTree(int? forumId = null, LoggedUser usr = null, bool excludePasswordProtected = false, bool fullTraversal = false)
        {
            if (_tree != null)
            {
                return _tree;
            }

            var passwordProtected = Enumerable.Empty<int>();
            if (excludePasswordProtected)
            {
                using var connection = _context.Database.GetDbConnection();
                await connection.OpenIfNeeded();
                passwordProtected = await connection.QueryAsync<int>("SELECT forum_id FROM phpbb_forums WHERE COALESCE(forum_password, '') <> ''");
            }

            var restrictedForums = (usr?.AllPermissions?.Where(p => p.AuthRoleId == Constants.ACCESS_TO_FORUM_DENIED_ROLE)?.Select(p => p.ForumId) ?? Enumerable.Empty<int>()).Union(passwordProtected);
            using (var connection = _context.Database.GetDbConnection())
            {
                _tree = new HashSet<ForumTree>(await connection.QueryAsync<ForumTree>(
                    "CALL `forum`.`get_forum_tree`(@restricted, @forumId, @fullTraversal);", 
                    new { restricted = string.Join(',', restrictedForums), forumId, fullTraversal }));
            }

            return _tree;
        }

        public async Task<List<ForumTree>> GetPathInTree(ForumTree root, int forumId)
        {
            var track = new List<ForumTree>();
            await Traverse(track, root, false, 0, x => x, (x, _) => { }, forumId, -1);
            return track;
        }

        public async Task<List<T>> GetPathInTree<T>(ForumTree root, Func<ForumTree, T> mapToType, int forumId, int topicId)
        {
            var track = new List<T>();
            await Traverse(track, root, false, 0, mapToType, (x, _) => { }, forumId, topicId);
            return track;
        }

        public async Task<List<T>> GetPathInTree<T>(ForumTree root, Func<ForumTree, T> mapToType)
        {
            var track = new List<T>();
            await Traverse(track, root, true, 0, mapToType, (x, _) => { }, -1, -1);
            return track;
        }

        public async Task<List<T>> GetPathInTree<T>(ForumTree root, Func<ForumTree, T> mapToType, Action<T, int> transformForLevel) where T : class
        {
            var track = new List<T>();
            await Traverse(track, root, true, 0, mapToType, transformForLevel, -1, -1);
            return track;
        }

        private async Task<bool> Traverse<T>(List<T> track, ForumTree node, bool isFullTraversal, int level, Func<ForumTree, T> mapToType, Action<T, int> transformForLevel, int forumId, int topicId) 
        {
            if (node == null)
            {
                return false;
            }

            T item = default;

            if (!isFullTraversal && ((node.TopicsList?.Any(t => t == topicId) ?? false) || node.ForumId == forumId))
            {
                item = mapToType(node);
                transformForLevel(item, level);
                track.Add(item);
                return true;
            }

            item = mapToType(node);
            transformForLevel(item, level);
            track.Add(item);

            foreach (var childId in node.ChildrenList)
            {
                var child = (await GetForumTree()).FirstOrDefault(t => t.ForumId == childId);
                if (await Traverse(track, child, isFullTraversal, level + 1, mapToType, transformForLevel, forumId, topicId))
                {
                    return true;
                }
            }

            if (!isFullTraversal)
            {
                track.RemoveAt(track.Count - 1);
            }
            return false;
        }
    }
}
