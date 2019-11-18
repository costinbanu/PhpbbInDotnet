using AutoMapper;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Serverless.Forum.Contracts;
using Serverless.Forum.forum;
using Serverless.Forum.Pages;
using Serverless.Forum.Utilities;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;

namespace Serverless.Forum
{
    public class ModelWithLoggedUser : PageModel
    {
        protected readonly Lazy<List<PhpbbAclRoles>> _adminRoles;
        protected readonly Lazy<List<PhpbbAclRoles>> _modRoles;
        protected readonly IConfiguration _config;
        protected readonly Utils _utils;
        private IEnumerable<Tracking> _tracking;

        private readonly Lazy<int?> _currentUserId;
        private ForumDisplay _tree = null;

        public ModelWithLoggedUser(IConfiguration config, Utils utils)
        {
            _config = config;
            _utils = utils;

            _adminRoles = new Lazy<List<PhpbbAclRoles>>(() =>
            {
                using (var context = new forumContext(config))
                {
                    return (from r in context.PhpbbAclRoles
                            where r.RoleType == "a_"
                            select r).ToList();
                }
            });

            _modRoles = new Lazy<List<PhpbbAclRoles>>(() =>
            {
                using (var context = new forumContext(config))
                {
                    return (from r in context.PhpbbAclRoles
                            where r.RoleType == "m_"
                            select r).ToList();
                }
            });

            _currentUserId = new Lazy<int?>(() => GetCurrentUserAsync().GetAwaiter().GetResult().UserId);
        }

        public async Task<LoggedUser> GetCurrentUserAsync()
        {
            var user = User;
            if (!user?.Identity?.IsAuthenticated ?? false)
            {
                using (var context = new forumContext(_config))
                {
                    user = _utils.Anonymous;
                }
                await HttpContext.SignInAsync(
                    CookieAuthenticationDefaults.AuthenticationScheme,
                    user,
                    new AuthenticationProperties
                    {
                        AllowRefresh = true,
                        ExpiresUtc = DateTimeOffset.UtcNow.AddMonths(1),
                        IsPersistent = true,
                    }
                );
            }
            return await user.ToLoggedUser(_utils);
        }

        public async Task<bool> IsCurrentUserAdminHere(int forumId)
        {
            return (from up in (await GetCurrentUserAsync()).UserPermissions
                    where up.ForumId == forumId || up.ForumId == 0
                    join a in _adminRoles.Value
                    on up.AuthRoleId equals a.RoleId
                    select up).Any();
        }

        public async Task<bool> IsCurrentUserModHere(int forumId)
        {
            return (from up in (await GetCurrentUserAsync()).UserPermissions
                    where up.ForumId == forumId || up.ForumId == 0
                    join a in _modRoles.Value
                    on up.AuthRoleId equals a.RoleId
                    select up).Any();
        }

        public async Task ReloadCurrentUser()
        {
            var current = (await GetCurrentUserAsync()).UserId;
            if (current != 1)
            {
                var result = await HttpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                await HttpContext.SignOutAsync();
                using (var context = new forumContext(_config))
                {
                    await HttpContext.SignInAsync(
                        CookieAuthenticationDefaults.AuthenticationScheme,
                        await _utils.LoggedUserFromDbUserAsync(
                            await context.PhpbbUsers.FirstAsync(u => u.UserId == current)
                        ),
                        new AuthenticationProperties
                        {
                            AllowRefresh = true,
                            ExpiresUtc = result.Properties.ExpiresUtc,
                            IsPersistent = true,
                        }
                    );
                }
            }
        }

        public int? CurrentUserId => _currentUserId.Value;

        //public async Task<Dictionary<int, List<int>>> GetUnreadTopicsAndAncestorsAsync()
        //{
        //    async Task<List<int>> ancestors(forumContext context, int current, List<int> parents)
        //    {
        //        var thisForum = await context.PhpbbForums.FirstOrDefaultAsync(f => f.ForumId == current);
        //        if (thisForum == null)
        //        {
        //            return parents;
        //        }
        //        parents.Add(current);
        //        return await ancestors(context, thisForum.ParentId, parents);
        //    }

        //    var unread = GetUnreadTopicsAndParentsLazy();
        //    using (var context = new forumContext(_config))
        //    {
        //        var toReturn = new Dictionary<int, List<int>>();
        //        foreach (var (ForumId, TopicId) in unread)
        //        {
        //            toReturn.Add(TopicId, await ancestors(context, ForumId, new List<int>()));
        //        }

        //        return toReturn;
        //    }
        //}

        public bool IsForumUnread(int forumId)
        {
            var unread = GetUnreadTopicsAndParentsLazy();
            return unread.Any(u => u.ForumId == forumId);
        }

        public bool IsTopicUnread(int topicId)
        {
            var unread = GetUnreadTopicsAndParentsLazy();
            return unread.Any(u => u.TopicId == topicId);
        }

        public bool IsPostUnread(int topicId, int PostId)
        {
            var unread = GetUnreadTopicsAndParentsLazy();
            return unread.FirstOrDefault(t => t.TopicId == topicId)?.Posts?.Any(p => p == PostId) ?? false;
        }

        public int GetFirstUnreadPost(int topicId)
        {
            var unread = GetUnreadTopicsAndParentsLazy();
            return unread.FirstOrDefault(t => t.TopicId == topicId)?.Posts?.FirstOrDefault() ?? 0;
        }

        public async Task<ForumDisplay> GetForumTree(ForumType? parentType = null)
        {
            if (_tree != null)
            {
                return _tree;
            }

            var usr = await GetCurrentUserAsync();
            using (var context = new forumContext(_config))
            {
                var allForums = await (
                    from f in context.PhpbbForums
                    where (parentType == null || f.ForumType == (byte?)parentType)
                       && usr.UserPermissions != null
                       && !usr.UserPermissions.Any(fp => fp.ForumId == f.ForumId && fp.AuthRoleId == 16)

                    orderby f.LeftId

                    join u in context.PhpbbUsers
                    on f.ForumLastPosterId equals u.UserId
                    into joinedUsers

                    join t in context.PhpbbTopics
                    on f.ForumId equals t.ForumId
                    into joinedTopics

                    from ju in joinedUsers.DefaultIfEmpty()

                    select new
                    {
                        ForumDisplay = new ForumDisplay
                        {
                            Id = f.ForumId,
                            Name = HttpUtility.HtmlDecode(f.ForumName),
                            Description = HttpUtility.HtmlDecode(f.ForumDesc),
                            LastPosterName = HttpUtility.HtmlDecode(f.ForumLastPosterName),
                            LastPosterId = ju.UserId == 1 ? null as int? : ju.UserId,
                            LastPostTime = f.ForumLastPostTime.TimestampToLocalTime(),
                            Unread = IsForumUnread(f.ForumId),
                            LastPosterColor = ju == null ? null : ju.UserColour,
                            Topics = (from jt in joinedTopics
                                      orderby jt.TopicLastPostTime descending
                                      select new TopicDisplay
                                      {
                                          Id = jt.TopicId,
                                          Title = HttpUtility.HtmlDecode(jt.TopicTitle),
                                      }).ToList()

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
                    return node;
                }

                _tree = new ForumDisplay
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

                return _tree;
            }
        }

        public async Task<List<int>> PathToForumOrTopic(int? forumId, int? topicId)
        {
            var track = new List<int>();

            bool traverse(ForumDisplay node)
            {
                if (node == null)
                {
                    return false;
                }

                if ((node.Topics?.Any(t => t.Id == topicId) ?? false) || node.Id == forumId)
                {
                    track.Add(node.Id.Value);
                    return true;
                }

                track.Add(node.Id.Value);

                foreach(var child in node.ChildrenForums)
                {
                    if (traverse(child))
                    {
                        return true;
                    }
                }

                track.RemoveAt(track.Count - 1);
                return false;
            }

            traverse(await GetForumTree());

            return track;
        }

        private bool greaterThanMarked(PhpbbForumsTrack ft, PhpbbTopicsTrack tt, long toCompare)
        {
            return (ft != null || tt != null) && !((tt != null && toCompare <= tt.MarkTime) || (ft != null && toCompare <= ft.MarkTime));
        }

        private IEnumerable<Tracking> GetUnreadTopicsAndParentsLazy()
        {


            if (_tracking == null)
            {
                //https://www.phpbb.com/community/viewtopic.php?t=2165146
                //https://www.phpbb.com/community/viewtopic.php?p=2987015
                using (var context = new forumContext(_config))
                {
                    _tracking = (
                        from t in context.PhpbbTopics
                        from u in context.PhpbbUsers

                            //let times = from p in context.PhpbbPosts
                            //            where p.TopicId == t.TopicId
                            //            select p.PostTime
                        let topicLastPostTime = t.TopicLastPostTime // times.Count() > 0 ? times.Max() : 0L

                        where u.UserId == (CurrentUserId ?? 0) && topicLastPostTime > u.UserLastmark

                        join tt in context.PhpbbTopicsTrack
                        on new { t.TopicId, UserId = CurrentUserId ?? 0 } equals new { tt.TopicId, tt.UserId }
                        into trackedTopics

                        join ft in context.PhpbbForumsTrack
                        on new { t.ForumId, UserId = CurrentUserId ?? 0 } equals new { ft.ForumId, ft.UserId }
                        into trackedForums

                        from tt in trackedTopics.DefaultIfEmpty()
                        from ft in trackedForums.DefaultIfEmpty()

                        where greaterThanMarked(ft, tt, topicLastPostTime)
                        //!((tt != null && topicLastPostTime <= tt.MarkTime) || (ft != null && topicLastPostTime <= ft.MarkTime))

                        select new Tracking
                        {
                            TopicId = t.TopicId,
                            ForumId = t.ForumId,
                            Posts = from p in context.PhpbbPosts
                                    where p.TopicId == t.TopicId
                                       && greaterThanMarked(ft, tt, p.PostTime)
                                    //!((tt != null && p.PostTime <= tt.MarkTime) || (ft != null && p.PostTime <= ft.MarkTime))
                                    orderby p.PostTime
                                    select p.PostId
                        }
                    ).ToList();
                }
            }
            return _tracking;
        }

        class Tracking
        {
            internal int TopicId { get; set; }
            internal int ForumId { get; set; }
            internal IEnumerable<int> Posts { get; set; }
        }
    }

    public enum ForumType
    {
        Category = 0,
        SubForum = 1
    }
}
