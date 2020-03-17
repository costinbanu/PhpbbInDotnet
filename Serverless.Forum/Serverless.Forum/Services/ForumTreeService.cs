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
        private readonly Utils _utils;

        public ForumTreeService(IConfiguration config, Utils utils)
        {
            _config = config;
            _utils = utils;
        }

        public async Task<ForumDisplay> GetForumTreeAsync(ForumType? parentType = null, LoggedUser usr = null, Func<int, bool> IsForumUnread = null)
        {
            using (var context = new ForumDbContext(_config))
            {
                if (IsForumUnread == null)
                {
                    IsForumUnread = new Func<int, bool>(_ => false);
                }

                var allForums = await (
                    from f in context.PhpbbForums.AsNoTracking()
                    where (parentType == null || f.ForumType == parentType)
                       && (usr == null || usr.UserPermissions == null || !usr.UserPermissions.Any(fp => fp.ForumId == f.ForumId && fp.AuthRoleId == 16))
                    orderby f.LeftId

                    //join u in context.PhpbbUsers
                    //on f.ForumLastPosterId equals u.UserId
                    //into joinedUsers

                    join t in context.PhpbbTopics
                    on f.ForumId equals t.ForumId
                    into joinedTopics

                    //from ju in joinedUsers.DefaultIfEmpty()

                    select new
                    {
                        ForumDisplay = new ForumDisplay
                        {
                            Id = f.ForumId,
                            ParentId = f.ParentId,
                            Name = HttpUtility.HtmlDecode(f.ForumName),
                            ForumPassword = f.ForumPassword,
                            Description = HttpUtility.HtmlDecode(f.ForumDesc),
                            Unread = IsForumUnread(f.ForumId),
                            LastPostId = f.ForumLastPostId,
                            LastPosterName = HttpUtility.HtmlDecode(f.ForumLastPosterName),
                            LastPosterId = /*ju.UserId*/f.ForumLastPosterId == 1 ? null as int? : /*ju.UserId*/f.ForumLastPosterId,
                            LastPostTime = f.ForumLastPostTime.ToUtcTime(),
                            LastPosterColor = /*ju == null ? null : ju.UserColour*/f.ForumLastPosterColour,
                            Topics = (from jt in joinedTopics
                                      orderby jt.TopicLastPostTime descending
                                      select new TopicDisplay
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

                ForumDisplay traverse(ForumDisplay node)
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

                return new ForumDisplay
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
        }

        public List<ForumDisplay> GetPathInTree(ForumDisplay root, int forumId)
        {
            var track = new List<ForumDisplay>();
            Traverse(track, root, false, 0, x => x, (x, _) => { }, forumId, -1);
            return track;
        }

        public List<T> GetPathInTree<T>(ForumDisplay root, Func<ForumDisplay, T> mapToType, int forumId, int topicId)
        {
            var track = new List<T>();
            Traverse(track, root, false, 0, mapToType, (x, _) => { }, forumId, topicId);
            return track;
        }

        public List<T> GetPathInTree<T>(ForumDisplay root, Func<ForumDisplay, T> mapToType, Action<T, int> transformForLevel, int forumId, int topicId) where T : class
        {
            var track = new List<T>();
            Traverse(track, root, false, 0, mapToType, transformForLevel, forumId, topicId);
            return track;
        }

        public List<T> GetPathInTree<T>(ForumDisplay root, Func<ForumDisplay, T> mapToType)
        {
            var track = new List<T>();
            Traverse(track, root, true, 0, mapToType, (x, _) => { }, -1, -1);
            return track;
        }

        public List<T> GetPathInTree<T>(ForumDisplay root, Func<ForumDisplay, T> mapToType, Action<T, int> transformForLevel) where T : class
        {
            var track = new List<T>();
            Traverse(track, root, true, 0, mapToType, transformForLevel, -1, -1);
            return track;
        }

        private bool Traverse<T>(List<T> track, ForumDisplay node, bool isFullTraversal, int level, Func<ForumDisplay, T> mapToType, Action<T, int> transformForLevel, int forumId, int topicId) 
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
