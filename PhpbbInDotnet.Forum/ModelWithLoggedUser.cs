using Dapper;
using DeviceDetectorNET;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Net.Http.Headers;
using PhpbbInDotnet.DTOs;
using PhpbbInDotnet.Database;
using PhpbbInDotnet.Database.Entities;
using PhpbbInDotnet.Services;
using PhpbbInDotnet.Utilities;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Web;

namespace PhpbbInDotnet.Forum
{
    public class ModelWithLoggedUser : PageModel
    {
        protected readonly ForumTreeService _forumService;
        protected readonly CacheService _cacheService;
        protected readonly UserService _userService;
        protected readonly ForumDbContext _context;
        protected readonly IConfiguration _config;
        protected readonly CommonUtils _utils;

        private readonly AnonymousSessionCounter _sessionCounter;
        private LoggedUser _currentUser;

        public ModelWithLoggedUser(ForumDbContext context, ForumTreeService forumService, UserService userService, CacheService cacheService, 
            IConfiguration config, AnonymousSessionCounter sessionCounter, CommonUtils utils)
        {
            _forumService = forumService;
            _cacheService = cacheService;
            _userService = userService;
            _context = context;
            _config = config;
            _sessionCounter = sessionCounter;
            _utils = utils;
        }

        #region User

        public async Task<LoggedUser> GetCurrentUserAsync()
        {
            if (_currentUser != null)
            {
                return _currentUser;
            }
            var user = User;
            var authenticationExpiryDays = _config.GetValue<int?>("LoginSessionSlidingExpirationDays") ?? 30;
            var sessionTrackingTimeoutMinutes = _config.GetValue<int?>("UserActivityTrackingIntervalMinutes") ?? 60;
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
                        ExpiresUtc = DateTimeOffset.UtcNow.Add(TimeSpan.FromDays(authenticationExpiryDays)),
                        IsPersistent = true,
                    }
                );
            }
            else
            {
                _currentUser = await _userService.ClaimsPrincipalToLoggedUserAsync(user);
                if (!_currentUser.IsAnonymous)
                {
                    var key = $"UserMustLogIn_{_currentUser.UsernameClean}";
                    if (await _cacheService.GetFromCache<bool?>(key) ?? false)
                    {
                        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                    }
                    else
                    {
                        var connection = _context.Database.GetDbConnection();
                        await connection.OpenIfNeededAsync();

                        var dbUser = await connection.QueryFirstOrDefaultAsync<PhpbbUsers>("SELECT * FROM phpbb_users WHERE user_id = @userId", new { _currentUser.UserId });
                        if (dbUser == null || dbUser.UserInactiveTime > 0)
                        {
                            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                            await _cacheService.SetInCache(key, true, TimeSpan.FromDays(authenticationExpiryDays));
                        }
                        else if (DateTime.UtcNow.Subtract(dbUser.UserLastvisit.ToUtcTime()) > TimeSpan.FromMinutes(sessionTrackingTimeoutMinutes))
                        {
                            await connection.ExecuteAsync("UPDATE phpbb_users SET user_lastvisit = @now WHERE user_id = @userId", new { now = DateTime.UtcNow.ToUnixTimestamp(), _currentUser.UserId });
                        }
                    }
                }
            }
            if (_currentUser.IsAnonymous && HttpContext.Request.Headers.TryGetValue(HeaderNames.UserAgent, out var header) && (HttpContext.Session.GetInt32("SessionCounted") ?? 0) == 0 )
            {
                try
                {
                    var dd = new DeviceDetector(header.ToString());
                    dd.Parse();
                    if (!dd.IsBot())
                    {
                        HttpContext.Session.SetInt32("SessionCounted", 1);
                        _sessionCounter.UpsertSession(HttpContext.Session.Id, TimeSpan.FromMinutes(sessionTrackingTimeoutMinutes));
                    }
                }
                catch (Exception ex)
                {
                    _utils.HandleError(ex, "Failed to detect anonymous session type.");
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
                var connection = _context.Database.GetDbConnection();
                await connection.OpenIfNeededAsync();

                var dbUser = await connection.QueryFirstOrDefaultAsync<PhpbbUsers>("SELECT * FROM phpbb_users WHERE user_id = @userId", new { userId = current });

                await HttpContext.SignInAsync(
                    CookieAuthenticationDefaults.AuthenticationScheme,
                    await _userService.DbUserToClaimsPrincipalAsync(dbUser),
                    new AuthenticationProperties
                    {
                        AllowRefresh = true,
                        ExpiresUtc = result.Properties.ExpiresUtc,
                        IsPersistent = true,
                    }
                );

            }
        }

        public async Task<bool> IsCurrentUserAdminHere(int forumId = 0)
            => await _userService.IsUserAdminInForum(await GetCurrentUserAsync(), forumId);

        public async Task<bool> IsCurrentUserModeratorHere(int forumId = 0)
            => await _userService.IsUserModeratorInForum(await GetCurrentUserAsync(), forumId);

        #endregion User

        #region Forum for user

        public async Task<bool> IsForumUnread(int forumId, bool forceRefresh = false)
        {
            var usr = await GetCurrentUserAsync();
            return !usr.IsAnonymous && await _forumService.IsForumUnread(forumId, usr, forceRefresh);
        }

        public async Task<bool> IsTopicUnread(int forumId, int topicId, bool forceRefresh = false)
        {
            var usr = await GetCurrentUserAsync();
            return !usr.IsAnonymous && await _forumService.IsTopicUnread(forumId, topicId, usr, forceRefresh);
        }

        public async Task<bool> IsPostUnread(int forumId, int topicId, int postId)
        {
            var usr = await GetCurrentUserAsync();
            return !usr.IsAnonymous && await _forumService.IsPostUnread(forumId, topicId, postId, usr);
        }

        public async Task<int> GetFirstUnreadPost(int forumId, int topicId)
        {
            if ((await GetCurrentUserAsync()).IsAnonymous)
            {
                return 0;
            }
            Tracking item = null;
            var found = (await GetForumTree()).Tracking.TryGetValue(forumId, out var tt) && tt.TryGetValue(new Tracking { TopicId = topicId }, out item);
            if (!found)
            {
                return 0;
            }

            using var connection = _context.Database.GetDbConnection();
            await connection.OpenIfNeededAsync();
            return unchecked((int)((await connection.QuerySingleOrDefaultAsync(
                "SELECT post_id, post_time FROM phpbb_posts WHERE post_id IN @postIds HAVING post_time = MIN(post_time)",
                new { postIds = item.Posts.DefaultIfEmpty() }
            ))?.post_id ?? 0u));
        }

        public async Task<(HashSet<ForumTree> Tree, Dictionary<int, HashSet<Tracking>> Tracking)> GetForumTree(bool forceRefresh = false)
            => (await _forumService.GetForumTree(await GetCurrentUserAsync(), forceRefresh), await _forumService.GetForumTracking(await GetCurrentUserAsync(), forceRefresh));

        protected async Task MarkForumAndSubforumsRead(int forumId)
        {
            var node = _forumService.GetTreeNode((await GetForumTree()).Tree, forumId);
            if (node == null)
            {
                if (forumId == 0)
                {
                    await SetLastMark();
                }
                return;
            }

            await MarkForumRead(forumId);
            foreach (var child in node.ChildrenList ?? new HashSet<int>())
            {
                await MarkForumAndSubforumsRead(child);
            }
        }

        protected async Task MarkForumRead(int forumId)
        {
            var usrId = (await GetCurrentUserAsync()).UserId;
            using var connection = _context.Database.GetDbConnection();
            await connection.OpenIfNeededAsync();

            await connection.ExecuteAsync(
                "DELETE FROM phpbb_topics_track WHERE forum_id = @forumId AND user_id = @usrId; " +
                "DELETE FROM phpbb_forums_track WHERE forum_id = @forumId AND user_id = @usrId; " +
                "INSERT INTO phpbb_forums_track (forum_id, user_id, mark_time) VALUES (@forumId, @usrId, @markTime);", 
                new { forumId, usrId, markTime = DateTime.UtcNow.ToUnixTimestamp() }
            );
        }

        protected async Task SetLastMark()
        {
            var usrId = (await GetCurrentUserAsync()).UserId;
            using var connection = _context.Database.GetDbConnection();
            await connection.OpenIfNeededAsync();
            await connection.ExecuteAsync("UPDATE phpbb_users SET user_lastmark = @markTime WHERE user_id = @usrId", new { markTime = DateTime.UtcNow.ToUnixTimestamp(), usrId });
        }

        #endregion Forum for user

        #region Permission validation wrappers

        protected async Task<IActionResult> WithRegisteredUser(Func<LoggedUser, Task<IActionResult>> toDo)
        {
            var user = await GetCurrentUserAsync();
            if (user.IsAnonymous)
            {
                return RedirectToPage("Login", new { ReturnUrl = HttpUtility.UrlEncode(HttpContext.Request.Path + HttpContext.Request.QueryString) });
            }
            return await toDo(user);
        }

        protected async Task<IActionResult> WithModerator(Func<Task<IActionResult>> toDo)
        {
            if (!await IsCurrentUserModeratorHere())
            {
                return RedirectToPage("Login", new { ReturnUrl = HttpUtility.UrlEncode(HttpContext.Request.Path + HttpContext.Request.QueryString) });
            }
            return await toDo();
        }

        protected async Task<IActionResult> WithAdmin(Func<Task<IActionResult>> toDo)
        {
            if (!await IsCurrentUserAdminHere())
            {
                return RedirectToPage("Login", new { ReturnUrl = HttpUtility.UrlEncode(HttpContext.Request.Path + HttpContext.Request.QueryString) });
            }
            return await toDo();
        }

        protected async Task<IActionResult> WithValidForum(int forumId, bool overrideCheck, Func<PhpbbForums, Task<IActionResult>> toDo)
        {
            using var connection = _context.Database.GetDbConnection();
            await connection.OpenIfNeededAsync();
            var curForum = await connection.QuerySingleOrDefaultAsync<PhpbbForums>("SELECT * FROM phpbb_forums WHERE forum_id = @forumId", new { forumId });

            if (!overrideCheck)
            {
                if (curForum == null)
                {
                    return RedirectToPage("Error", new { isNotFound = true });
                }

                var usr = await GetCurrentUserAsync();
                var restricted = await _forumService.GetRestrictedForumList(usr, true);
                var tree = await _forumService.GetForumTree(usr, false);
                var path = new List<int>();
                if (tree.TryGetValue(new ForumTree { ForumId = forumId }, out var cur))
                {
                    path = cur?.PathList ?? new List<int>();
                }    
                var restrictedAncestor = (
                    from t in path
                    join r in restricted
                    on t equals r.forumId
                    into joined
                    from j in joined
                    where !j.hasPassword || (HttpContext.Session.GetInt32($"ForumLogin_{t}") ?? 0) != 1
                    select t
                ).FirstOrDefault();

                if (restrictedAncestor != default)
                {
                    if (usr?.AllPermissions?.Contains(new LoggedUser.Permissions { ForumId = restrictedAncestor, AuthRoleId = Constants.ACCESS_TO_FORUM_DENIED_ROLE }) ?? false)
                    {
                        return RedirectToPage("Index");
                    }
                    else
                    {
                        return RedirectToPage("ForumLogin", new
                        {
                            ReturnUrl = HttpUtility.UrlEncode(HttpContext.Request.Path + HttpContext.Request.QueryString),
                            ForumId = restrictedAncestor
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
            using var connection = _context.Database.GetDbConnection();
            await connection.OpenIfNeededAsync();
            
            var curTopic = await connection.QuerySingleOrDefaultAsync<PhpbbTopics>("SELECT * FROM phpbb_topics WHERE topic_id = @topicId", new { topicId });
            
            if (curTopic == null)
            {
                return RedirectToPage("Error", new { isNotFound = true });
            }
            return await WithValidForum(curTopic.ForumId, async (curForum) => await toDo(curForum, curTopic));
        }

        protected async Task<IActionResult> WithValidPost(int postId, Func<PhpbbForums, PhpbbTopics, PhpbbPosts, Task<IActionResult>> toDo)
        {
            using var connection = _context.Database.GetDbConnection();
            await connection.OpenIfNeededAsync();

            var curPost = await connection.QuerySingleOrDefaultAsync<PhpbbPosts>("SELECT * FROM phpbb_posts WHERE post_id = @postId", new { postId });
            if (curPost == null)
            {
                return RedirectToPage("Error", new { isNotFound = true });
            }
            return await WithValidTopic(curPost.TopicId, async (curForum, curTopic) => await toDo(curForum, curTopic, curPost));
        }

        #endregion Permission validation wrappers
    }
}
