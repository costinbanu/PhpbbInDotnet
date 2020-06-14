using Dapper;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Serverless.Forum.Contracts;
using Serverless.Forum.ForumDb;
using Serverless.Forum.Pages;
using Serverless.Forum.Services;
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
        protected readonly ForumTreeService _forumService;
        protected readonly CacheService _cacheService;
        protected readonly UserService _userService;
        protected readonly ForumDbContext _context;

        private IEnumerable<Tracking> _tracking;
        private LoggedUser _currentUser;
        private ForumDto _tree = null;

        public ModelWithLoggedUser(ForumDbContext context, ForumTreeService forumService, UserService userService, CacheService cacheService)
        {
            _forumService = forumService;
            _cacheService = cacheService;
            _userService = userService;
            _context = context;
        }

        #region User

        public async Task<LoggedUser> GetCurrentUserAsync()
        {
            if (_currentUser != null)
            {
                return _currentUser;
            }
            var user = User;
            if (!(user?.Identity?.IsAuthenticated ?? false))
            {
                user = await _userService.GetAnonymousClaimsPrincipalAsync();
                _currentUser = await _userService.ClaimsPrincipalToLoggedUserAsync(user);
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
                _currentUser = await _userService.ClaimsPrincipalToLoggedUserAsync(user);
                if (_currentUser != await _userService.GetAnonymousLoggedUserAsync())
                {
                    var key = $"UserMustLogIn_{_currentUser.UsernameClean}";
                    if (await _cacheService.GetFromCache<bool?>(key) ?? false)
                    {
                        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                    }
                    else
                    {
                        var dbUser = await _context.PhpbbUsers.AsNoTracking().FirstOrDefaultAsync(u => u.UserId == _currentUser.UserId);
                        if (dbUser == null || dbUser.UserInactiveTime > 0)
                        {
                            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                            await _cacheService.SetInCache(key, true);
                        }
                    }
                }
            }
            return _currentUser;
        }

        public async Task ReloadCurrentUser()
        {
            var current = (await GetCurrentUserAsync()).UserId;
            if (current != Constants.ANONYMOUS_USER_ID)
            {
                var result = await HttpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                await HttpContext.SignOutAsync();

                await HttpContext.SignInAsync(
                    CookieAuthenticationDefaults.AuthenticationScheme,
                    await _userService.DbUserToClaimsPrincipalAsync(await _context.PhpbbUsers.AsNoTracking().FirstAsync(u => u.UserId == current)),
                    new AuthenticationProperties
                    {
                        AllowRefresh = true,
                        ExpiresUtc = result.Properties.ExpiresUtc,
                        IsPersistent = true,
                    }
                );

            }
        }

        public async Task<bool> IsCurrentUserAdminHereAsync(int forumId = 0)
            => await _userService.IsUserAdminInForum(await GetCurrentUserAsync(), forumId);

        public async Task<bool> IsCurrentUserModeratorHereAsync(int forumId = 0)
            => await _userService.IsUserModeratorInForum(await GetCurrentUserAsync(), forumId);

        public int CurrentUserId => GetCurrentUserAsync().RunSync().UserId;

        #endregion User

        #region Forum for user

        public bool IsForumUnread(int forumId, bool forceRefresh = false)
        {
            if (CurrentUserId == Constants.ANONYMOUS_USER_ID)
            {
                return false;
            }
            var unread = GetUnreadTopicsAndParentsLazy(forceRefresh);
            return unread.Any(u => u.ForumId == forumId);
        }

        public bool IsTopicUnread(int topicId, bool forceRefresh = false)
        {
            if (CurrentUserId == Constants.ANONYMOUS_USER_ID)
            {
                return false;
            }
            var unread = GetUnreadTopicsAndParentsLazy(forceRefresh);
            return unread.Any(u => u.TopicId == topicId);
        }

        public bool IsPostUnread(int topicId, int PostId)
        {
            if (CurrentUserId == Constants.ANONYMOUS_USER_ID)
            {
                return false;
            }
            var unread = GetUnreadTopicsAndParentsLazy();
            return unread.FirstOrDefault(t => t.TopicId == topicId)?.Posts?.Any(p => p == PostId) ?? false;
        }

        public int GetFirstUnreadPost(int topicId)
        {
            if (CurrentUserId == Constants.ANONYMOUS_USER_ID)
            {
                return 0;
            }
            var unread = GetUnreadTopicsAndParentsLazy();
            return unread.FirstOrDefault(t => t.TopicId == topicId)?.Posts?.FirstOrDefault() ?? 0;
        }

        public async Task<ForumDto> GetForumTreeAsync(ForumType? parentType = null, bool forceRefresh = false)
        {
            if (forceRefresh || _tree == null)
            {
                _tree = await _forumService.GetForumTree(parentType, await GetCurrentUserAsync(), forumId => IsForumUnread(forumId, forceRefresh));
            }
            return _tree;
        } 

        public async Task<List<int>> PathToForumOrTopic(int forumId, int? topicId = null)
            => _forumService.GetPathInTree(await GetForumTreeAsync(), forum => forum.Id ?? 0, forumId, topicId ?? -1);

        public async Task<ForumDto> GetForum(int forumId, bool forceRefresh = false)
            => _forumService.GetPathInTree(await GetForumTreeAsync(forceRefresh: forceRefresh), forumId).Last();

        protected IEnumerable<Tracking> GetUnreadTopicsAndParentsLazy(bool forceRefresh = false)
        {
            if (_tracking != null && !forceRefresh)
            {
                return _tracking;
            }

            using (var connection = _context.Database.GetDbConnection())
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

        #endregion Forum for user

        #region Permission validation wrappers

        protected async Task<IActionResult> WithRegisteredUser(Func<Task<IActionResult>> toDo)
        {
            if (await GetCurrentUserAsync() == await _userService.GetAnonymousLoggedUserAsync())
            {
                return RedirectToPage("Login", new { ReturnUrl = HttpUtility.UrlEncode(HttpContext.Request.Path + HttpContext.Request.QueryString) });
            }
            return await toDo();
        }

        protected async Task<IActionResult> WithModerator(Func<Task<IActionResult>> toDo)
        {
            if (!await IsCurrentUserModeratorHereAsync())
            {
                return Forbid();
            }
            return await toDo();
        }

        protected async Task<IActionResult> WithAdmin(Func<Task<IActionResult>> toDo)
        {
            var validationResult = !await IsCurrentUserAdminHereAsync() ? Forbid() : null;
            if (validationResult != null)
            {
                return validationResult;
            }
            return await toDo();
        }

        protected async Task<IActionResult> WithValidForum(int forumId, bool overrideCheck, Func<PhpbbForums, Task<IActionResult>> toDo)
        {
            var curForum = await _context.PhpbbForums.AsNoTracking().FirstOrDefaultAsync(f => f.ForumId == forumId);
            if (!overrideCheck)
            {
                if (curForum == null)
                {
                    return NotFound($"Forumul solicitat nu există.");
                }

                var forumAncestors = _forumService.GetPathInTree(await GetForumTreeAsync(), curForum.ForumId);
                var restrictedAncestor = forumAncestors.FirstOrDefault(
                    f => !string.IsNullOrEmpty(f.ForumPassword) && (HttpContext.Session.GetInt32($"ForumLogin_{f.Id}") ?? 0) != 1);

                if (restrictedAncestor != null)
                {
                    if ((await GetCurrentUserAsync())?.AllPermissions?.Any(p => p.ForumId == restrictedAncestor.Id && p.AuthRoleId == Constants.ACCESS_TO_FORUM_DENIED_ROLE) ?? false)
                    {
                        return Unauthorized();
                    }
                    else
                    {
                        return RedirectToPage("ForumLogin", new ForumLoginModel(_context)
                        {
                            ReturnUrl = HttpUtility.UrlEncode(HttpContext.Request.Path + HttpContext.Request.QueryString),
                            ForumId = restrictedAncestor.Id.Value,
                            ForumName = restrictedAncestor.Name
                        });
                    }
                }
            }
            return await toDo(curForum);
        }

        protected async Task<IActionResult> WithValidForum(int forumId, Func<PhpbbForums, Task<IActionResult>> toDo)
            => await WithValidForum(forumId, false, toDo);

        protected async Task<IActionResult> WithValidTopic(int topicId, Func<PhpbbForums, PhpbbTopics, Task<IActionResult>> toDo)
        { 
            var curTopic = await _context.PhpbbTopics.AsNoTracking().FirstOrDefaultAsync(t => t.TopicId == topicId);
            if (curTopic == null)
            {
                return NotFound();
            }
            return await WithValidForum(curTopic.ForumId, async (curForum) => await toDo(curForum, curTopic));
        }

        protected async Task<IActionResult> WithValidPost(int postId, Func<PhpbbForums, PhpbbTopics, PhpbbPosts, Task<IActionResult>> toDo)
        {
            var curPost = await _context.PhpbbPosts.AsNoTracking().FirstOrDefaultAsync(p => p.PostId == postId);
            if (curPost == null)
            {
                return NotFound();
            }
            return await WithValidTopic(curPost.TopicId, async (curForum, curTopic) => await toDo(curForum, curTopic, curPost));
        }

        #endregion Permission validation wrappers

        protected class Tracking
        {
            internal int TopicId { get; set; }
            internal int ForumId { get; set; }
            internal IEnumerable<int> Posts { get; set; }
        }

        protected class TrackingQueryResult
        {
            internal int ForumId { get; set; }
            internal int TopicId { get; set; }
            internal int PostId { get; set; }
        }
    }
}
