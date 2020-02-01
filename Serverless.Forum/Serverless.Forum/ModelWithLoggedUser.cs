using Dapper;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
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

namespace Serverless.Forum
{
    public class ModelWithLoggedUser : PageModel
    {

        protected readonly IConfiguration _config;
        protected readonly Utils _utils;

        private IEnumerable<Tracking> _tracking;
        private LoggedUser _currentUser;
        private ForumDisplay _tree = null;

        public ModelWithLoggedUser(IConfiguration config, Utils utils)
        {
            _config = config;
            _utils = utils;
            //_currentUser = new Lazy<LoggedUser>(() => GetCurrentUserAsync().RunSync());
        }

        public async Task<LoggedUser> GetCurrentUserAsync()
        {
            if (_currentUser != null)
            {
                return _currentUser;
            }
            var user = User;
            //LoggedUser loggedUser = null;
            if (!(user?.Identity?.IsAuthenticated ?? false))
            {
                user = _utils.AnonymousClaimsPrincipal;
                _currentUser = await user.ToLoggedUserAsync(_utils);
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
            else
            {
                _currentUser = await user.ToLoggedUserAsync(_utils);
                if (_currentUser != _utils.AnonymousLoggedUser)
                {
                    var key = $"UserMustLogIn_{_currentUser.UsernameClean}";
                    if (await _utils.GetFromCacheAsync<bool?>(key) ?? false)
                    {
                        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                    }
                    else
                    {
                        using (var context = new ForumDbContext(_config))
                        {
                            var dbUser = await context.PhpbbUsers.FirstOrDefaultAsync(u => u.UserId == _currentUser.UserId);
                            if (dbUser == null || dbUser.UserInactiveTime > 0)
                            {
                                await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                                await _utils.SetInCacheAsync(key, true);
                            }
                        }
                    }
                }
            }
            return _currentUser;
        }

        public async Task ReloadCurrentUser()
        {
            var current = (await GetCurrentUserAsync()).UserId;
            if (current != 1)
            {
                var result = await HttpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                await HttpContext.SignOutAsync();
                using (var context = new ForumDbContext(_config))
                {
                    await HttpContext.SignInAsync(
                        CookieAuthenticationDefaults.AuthenticationScheme,
                        await (await context.PhpbbUsers.FirstAsync(u => u.UserId == current)).ToClaimsPrincipalAsync(context, _utils),
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

        public async Task<bool> IsCurrentUserAdminHereAsync(int forumId = 0) => _utils.IsUserAdminInForum(await GetCurrentUserAsync(), forumId);

        public async Task<bool> IsCurrentUserModeratorHereAsync(int forumId = 0) => _utils.IsUserModeratorInForum(await GetCurrentUserAsync(), forumId);

        public int CurrentUserId => GetCurrentUserAsync().RunSync().UserId;

        public bool IsForumUnread(int forumId)
        {
            if (CurrentUserId == 1)
            {
                return false;
            }
            var unread = GetUnreadTopicsAndParentsLazy();
            return unread.Any(u => u.ForumId == forumId);
        }

        public bool IsTopicUnread(int topicId)
        {
            if (CurrentUserId == 1)
            {
                return false;
            }
            var unread = GetUnreadTopicsAndParentsLazy();
            return unread.Any(u => u.TopicId == topicId);
        }

        public bool IsPostUnread(int topicId, int PostId)
        {
            if (CurrentUserId == 1)
            {
                return false;
            }
            var unread = GetUnreadTopicsAndParentsLazy();
            return unread.FirstOrDefault(t => t.TopicId == topicId)?.Posts?.Any(p => p == PostId) ?? false;
        }

        public int GetFirstUnreadPost(int topicId)
        {
            if (CurrentUserId == 1)
            {
                return 0;
            }
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
            using (var context = new ForumDbContext(_config))
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
                            LastPostTime = f.ForumLastPostTime.ToUtcTime(),
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

        private IEnumerable<Tracking> GetUnreadTopicsAndParentsLazy()
        {
            if (_tracking != null)
            {
                return _tracking;
            }

            using (var context = new ForumDbContext(_config))
            using (var connection = context.Database.GetDbConnection())
            {
                connection.Open();
                DefaultTypeMap.MatchNamesWithUnderscores = true;

                var result = connection.Query<TrackingQueryResult>(
                    "CALL `forum`.`get_post_tracking`(@userId, @topicId);", 
                    new { userId = CurrentUserId, topicId = null as int? }
                );
                _tracking = from t in result
                            group t by new { t.ForumId, t.TopicId } into grouped
                            select new Tracking
                            {
                                ForumId = grouped.Key.ForumId,
                                TopicId = grouped.Key.TopicId,
                                Posts = grouped.Select(g => g.PostId)
                            };
            }
            return _tracking;
        }

        protected async Task<IActionResult> ValidatePermissionsResponses(PhpbbForums thisForum, int forumId)
        {
            if (thisForum == null)
            {
                return NotFound($"Forumul {forumId} nu există.");
            }

            if (!string.IsNullOrEmpty(thisForum.ForumPassword) &&
                (HttpContext.Session.GetInt32("ForumLogin") ?? -1) != forumId)
            {
                if ((await GetCurrentUserAsync()).UserPermissions.Any(fp => fp.ForumId == forumId && fp.AuthRoleId == 16))
                {
                    return Unauthorized();
                }
                else
                {
                    return RedirectToPage("ForumLogin", new ForumLoginModel(_config)
                    {
                        ReturnUrl = HttpUtility.UrlEncode(HttpContext.Request.Path + HttpContext.Request.QueryString),
                        ForumId = forumId,
                        ForumName = thisForum.ForumName
                    });
                }
            }

            return null;
        }

        class Tracking
        {
            internal int TopicId { get; set; }
            internal int ForumId { get; set; }
            internal IEnumerable<int> Posts { get; set; }
        }

        class TrackingQueryResult
        {
            internal int ForumId { get; set; }
            internal int TopicId { get; set; }
            internal int PostId { get; set; }
        }
    }
}
