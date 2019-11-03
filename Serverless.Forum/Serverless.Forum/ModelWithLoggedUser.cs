﻿using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Serverless.Forum.Contracts;
using Serverless.Forum.forum;
using Serverless.Forum.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

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

            _currentUserId = new Lazy<int?>(() => GetCurrentUserAsync().RunSync().UserId);
        }

        public async Task<LoggedUser> GetCurrentUserAsync()
        {
            var user = User;
            if (!user.Identity.IsAuthenticated)
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

        private bool greaterThanMarked(PhpbbForumsTrack ft, PhpbbTopicsTrack tt, long toCompare)
        {
            return (ft!= null || tt != null) && !((tt != null && toCompare <= tt.MarkTime) || (ft != null && toCompare <= ft.MarkTime));
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
}
