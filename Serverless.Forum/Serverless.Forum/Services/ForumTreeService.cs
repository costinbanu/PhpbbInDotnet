using Microsoft.CodeAnalysis.CSharp;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Serverless.Forum.Contracts;
using Serverless.Forum.ForumDb;
using Serverless.Forum.Pages;
using Serverless.Forum.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;

namespace Serverless.Forum.Services
{
    public class ForumTreeService
    {
        private readonly IConfiguration _config;
        private readonly ForumDbContext _context;

        public ForumTreeService(IConfiguration config, ForumDbContext context)
        {
            _config = config;
            _context = context;
        }
        todo: this does not account for child forums; 
        public IEnumerable<int> GetRestrictedForumList(LoggedUser user, bool includePasswordProtected = true)
            => (user?.AllPermissions?.Where(p => p.AuthRoleId == Constants.ACCESS_TO_FORUM_DENIED_ROLE)?.Select(p => p.ForumId) ?? Enumerable.Empty<int>())
                .Union(includePasswordProtected ? _context.PhpbbForums.AsNoTracking().Where(f => !string.IsNullOrWhiteSpace(f.ForumPassword)).Select(f => f.ForumId) : Enumerable.Empty<int>());

        public async Task<ForumDto> GetForumTree(ForumType? parentType = null, LoggedUser usr = null, Func<int, bool> IsForumUnread = null)
        {
            if (IsForumUnread == null)
            {
                IsForumUnread = new Func<int, bool>(_ => false);
            }

            var restrictedForums = GetRestrictedForumList(usr, false).ToList();
            var allForums = await (
                from f in _context.PhpbbForums.AsNoTracking()
                where (parentType == null || f.ForumType == parentType)
                   //&& (usr == null || usr.AllPermissions == null || !usr.AllPermissions.Any(fp => fp.ForumId == f.ForumId && fp.AuthRoleId == 16))
                orderby f.LeftId

                join rf in restrictedForums
                on f.ForumId equals rf
                into joinedRestrictedForums

                from rf in joinedRestrictedForums.DefaultIfEmpty()
                where rf == default

                join t in _context.PhpbbTopics.AsNoTracking()
                on f.ForumId equals t.ForumId
                into joinedTopics

                select new
                {
                    ForumDisplay = new ForumDto
                    {
                        Id = f.ForumId,
                        ParentId = f.ParentId,
                        Name = HttpUtility.HtmlDecode(f.ForumName),
                        ForumPassword = f.ForumPassword,
                        Description = f.ForumDesc,
                        DescriptionBbCodeUid = f.ForumDescUid,
                        Unread = IsForumUnread(f.ForumId),
                        LastPostId = f.ForumLastPostId,
                        LastPosterName = HttpUtility.HtmlDecode(f.ForumLastPosterName),
                        LastPosterId = f.ForumLastPosterId == 1 ? null as int? : f.ForumLastPosterId,
                        LastPostTime = f.ForumLastPostTime.ToUtcTime(),
                        LastPosterColor = f.ForumLastPosterColour,
                        Topics = (from jt in joinedTopics
                                  orderby jt.TopicLastPostTime descending
                                  select new TopicDto
                                  {
                                      Id = jt.TopicId,
                                      Title = HttpUtility.HtmlDecode(jt.TopicTitle),
                                  }).ToList(),
                        ForumType = f.ForumType
                    },
                    Parent = f.ParentId,
                    Order = f.LeftId
                }
            ).ToListAsync();

            ForumDto traverse(ForumDto node)
            {
                node.ChildrenForums = (
                    from f in allForums
                    where f.Parent == node.Id
                    orderby f.Order
                    select traverse(f.ForumDisplay)
                ).ToList();

                var (lastPosterColor, lastPosterId, lastPosterName, lastPostId, lastPostTime) = (
                    from c in node.ChildrenForums
                    group c by c.LastPostTime into groups
                    orderby groups.Key ?? DateTime.MinValue descending
                    let first = groups.FirstOrDefault()
                    select (first?.LastPosterColor, first?.LastPosterId, first?.LastPosterName, first?.LastPostId, first?.LastPostTime)
                ).FirstOrDefault();

                node.Unread |= node.ChildrenForums.Any(c => c.Unread);
                if (node.LastPostTime < (lastPostTime ?? DateTime.MinValue))
                {
                    node.LastPosterColor = lastPosterColor ?? node.LastPosterColor;
                    node.LastPosterId = lastPosterId ?? node.LastPosterId;
                    node.LastPosterName = lastPosterName ?? node.LastPosterName;
                    node.LastPostId = lastPostId ?? node.LastPostId;
                    node.LastPostTime = lastPostTime ?? node.LastPostTime;
                }

                return node;
            }

            return new ForumDto
            {
                Id = 0,
                Name = Constants.FORUM_NAME,
                ChildrenForums = (
                    from f in allForums
                    where f.Parent == 0
                    orderby f.Order
                    select traverse(f.ForumDisplay)
                ).ToList()
            };
        }

        public List<ForumDto> GetPathInTree(ForumDto root, int forumId)
        {
            var track = new List<ForumDto>();
            Traverse(track, root, false, 0, x => x, (x, _) => { }, forumId, -1);
            return track;
        }

        public List<T> GetPathInTree<T>(ForumDto root, Func<ForumDto, T> mapToType, int forumId, int topicId)
        {
            var track = new List<T>();
            Traverse(track, root, false, 0, mapToType, (x, _) => { }, forumId, topicId);
            return track;
        }

        public List<T> GetPathInTree<T>(ForumDto root, Func<ForumDto, T> mapToType, Action<T, int> transformForLevel, int forumId, int topicId) where T : class
        {
            var track = new List<T>();
            Traverse(track, root, false, 0, mapToType, transformForLevel, forumId, topicId);
            return track;
        }

        public List<T> GetPathInTree<T>(ForumDto root, Func<ForumDto, T> mapToType)
        {
            var track = new List<T>();
            Traverse(track, root, true, 0, mapToType, (x, _) => { }, -1, -1);
            return track;
        }

        public List<T> GetPathInTree<T>(ForumDto root, Func<ForumDto, T> mapToType, Action<T, int> transformForLevel) where T : class
        {
            var track = new List<T>();
            Traverse(track, root, true, 0, mapToType, transformForLevel, -1, -1);
            return track;
        }

        private bool Traverse<T>(List<T> track, ForumDto node, bool isFullTraversal, int level, Func<ForumDto, T> mapToType, Action<T, int> transformForLevel, int forumId, int topicId) 
        {
            if (node == null)
            {
                return false;
            }

            T item = default;

            if (!isFullTraversal && ((node.Topics?.Any(t => t.Id == topicId) ?? false) || node.Id == forumId))
            {
                item = mapToType(node);
                transformForLevel(item, level);
                track.Add(item);
                return true;
            }

            item = mapToType(node);
            transformForLevel(item, level);
            track.Add(item);

            foreach (var child in node.ChildrenForums)
            {
                if (Traverse(track, child, isFullTraversal, level + 1, mapToType, transformForLevel, forumId, topicId))
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
