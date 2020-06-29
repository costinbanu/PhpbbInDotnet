using Dapper;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.EntityFrameworkCore;
using Serverless.Forum.Contracts;
using Serverless.Forum.ForumDb;
using Serverless.Forum.Pages;
using Serverless.Forum.Services;
using Serverless.Forum.Utilities;
using System;
using System.Collections.Generic;
using System.Data;
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

        private LoggedUser _currentUser;
        private ForumDto _tree = null;
        private ForumTree _root = null;
        private bool _wasFullTraversal = false;

        public ModelWithLoggedUser(ForumDbContext context, ForumTreeService forumService, UserService userService, CacheService cacheService)
        {
            _forumService = forumService;
            _cacheService = cacheService;
            _userService = userService;
            _context = context;
            DefaultTypeMap.MatchNamesWithUnderscores = true;
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
                if (_currentUser.UserId != Constants.ANONYMOUS_USER_ID)
                {
                    var key = $"UserMustLogIn_{_currentUser.UsernameClean}";
                    if (await _cacheService.GetFromCache<bool?>(key) ?? false)
                    {
                        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                    }
                    else
                    {
                        using var connection = _context.Database.GetDbConnection();
                        await connection.OpenIfNeeded();
                        var dbUser = await connection.QuerySingleOrDefaultAsync<PhpbbUsers>("SELECT * FROM phpbb_users where user_id = @UserId", new { _currentUser.UserId });
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

        //public int (await GetCurrentUserAsync()).UserId => GetCurrentUserAsync().RunSync().UserId;

        #endregion User

        #region Forum for user

        public async Task<bool> IsForumUnread(int forumId, bool forceRefresh = false)
        {
            if ((await GetCurrentUserAsync()).UserId == Constants.ANONYMOUS_USER_ID)
            {
                return false;
            }
           return (await GetForumTree(forceRefresh: forceRefresh)).Tracking.Any(f => f.ForumId == forumId);
        }

        public async Task<bool> IsTopicUnread(int topicId, bool forceRefresh = false)
        {
            if ((await GetCurrentUserAsync()).UserId == Constants.ANONYMOUS_USER_ID)
            {
                return false;
            }
            return (await GetForumTree(forceRefresh: forceRefresh)).Tracking.Any(f => f.TopicId == topicId);
        }

        public async Task<bool> IsPostUnread(int topicId, int postId)
        {
            if ((await GetCurrentUserAsync()).UserId == Constants.ANONYMOUS_USER_ID)
            {
                return false;
            }
            return (await GetForumTree()).Tracking.Any(f => f.TopicId == topicId && f.Posts.Contains(postId));
        }

        public async Task<int> GetFirstUnreadPost(int topicId)
        {
            if ((await GetCurrentUserAsync()).UserId == Constants.ANONYMOUS_USER_ID)
            {
                return 0;
            }
            return (await GetForumTree()).Tracking.FirstOrDefault(t => t.TopicId == topicId)?.Posts?.FirstOrDefault() ?? 0;
        }

        public async Task<ForumDto> GetForumTree(int? forumId = null, bool forceRefresh = false, bool fullTraversal = false)
        {
            if (forceRefresh || _tree == null || (fullTraversal && !_wasFullTraversal))
            {
                var usr = await GetCurrentUserAsync();
                var (tree, forums, topics, tracking) = await _forumService.GetExtendedForumTree(forumId, usr, forceRefresh, fullTraversal);
                _tree = new ForumDto(tree.FirstOrDefault(), tree, forums, topics, tracking);
                _wasFullTraversal = fullTraversal;
            }
            return _tree;
        }

        public async Task<ForumTree> GetForumRoot(bool forceRefresh = false)
        {
            if (forceRefresh || _root == null)
            {
                _root = (await GetForumTree(forceRefresh: forceRefresh)).Tree.FirstOrDefault(f => f.Level == 0);
            }
            return _root;
        }

        //public async Task<List<int>> PathToForumOrTopic(int forumId, int? topicId = null)
        //{
        //    //_forumService.GetPathInTree(await GetForumTreeAsync(), forum => forum.Id ?? 0, forumId, topicId ?? -1);
        //    var entry = (await GetForumTreeAsync()).FirstOrDefault(f => f.ForumId == forumId);
        //    var path = new List<int>(entry.PathList);
        //    if (topicId.HasValue && entry.TopicList.Any(t => t == topicId.Value))
        //    {
        //        path.Add(topicId.Value);
        //    }
        //    return path;
        //}

        //public async Task<ForumDto> GetForum(int forumId, bool forceRefresh = false)
        //    => _forumService.GetPathInTree(await GetForumTreeAsync(forceRefresh: forceRefresh), forumId).Last();

        //protected IEnumerable<Tracking> GetUnreadTopicsAndParentsLazy(bool forceRefresh = false)
        //{
        //    if (_tracking != null && !forceRefresh)
        //    {
        //        return _tracking;
        //    }
        //    var curUserId = (await GetCurrentUserAsync()).UserId;
        //    using (var connection = _context.Database.GetDbConnection())
        //    {
        //        if (connection.State != ConnectionState.Open)
        //        {
        //            connection.Open();
        //        }
        //        DefaultTypeMap.MatchNamesWithUnderscores = true;

        //        var result = connection.Query<Tracking>(
        //            "CALL `forum`.`get_post_tracking`(@userId, @topicId);",
        //            new { userId = curUserId, topicId = null as int? }
        //        );
        //        _tracking = from t in result
        //                    group t by new { t.ForumId, t.TopicId } into grouped
        //                    select new Tracking
        //                    {
        //                        ForumId = grouped.Key.ForumId,
        //                        TopicId = grouped.Key.TopicId,
        //                        Posts = grouped.Select(g => g.PostId)
        //                    };
        //    }
        //    return _tracking;
        //}

        #endregion Forum for user

        #region Permission validation wrappers

        protected async Task<IActionResult> WithRegisteredUser(Func<Task<IActionResult>> toDo)
        {
            if ((await GetCurrentUserAsync()).UserId == Constants.ANONYMOUS_USER_ID)
            {
                return RedirectToPage("Login", new { ReturnUrl = HttpUtility.UrlEncode(HttpContext.Request.Path + HttpContext.Request.QueryString) });
            }
            return await toDo();
        }

        protected async Task<IActionResult> WithModerator(Func<Task<IActionResult>> toDo)
        {
            if (!await IsCurrentUserModeratorHereAsync())
            {
                return RedirectToPage("Login", new { ReturnUrl = HttpUtility.UrlEncode(HttpContext.Request.Path + HttpContext.Request.QueryString) });
            }
            return await toDo();
        }

        protected async Task<IActionResult> WithAdmin(Func<Task<IActionResult>> toDo)
        {
            if (!await IsCurrentUserAdminHereAsync())
            {
                return RedirectToPage("Login", new { ReturnUrl = HttpUtility.UrlEncode(HttpContext.Request.Path + HttpContext.Request.QueryString) });
            }
            return await toDo();
        }

        protected async Task<IActionResult> WithValidForum(int forumId, bool overrideCheck, Func<PhpbbForums, Task<IActionResult>> toDo)
        {
            //var curForum = await _context.PhpbbForums.AsNoTracking().FirstOrDefaultAsync(f => f.ForumId == forumId);
            PhpbbForums curForum;
            using (var connection = _context.Database.GetDbConnection())
            {
                await connection.OpenIfNeeded();
                curForum = await connection.QuerySingleOrDefaultAsync<PhpbbForums>("SELECT * FROM phpbb_forums WHERE forum_id = @forumId", new { forumId });
            }
            if (!overrideCheck)
            {
                if (curForum == null)
                {
                    return NotFound($"Forumul solicitat nu există.");
                }

                //var treeAncestors = await _forumService.GetPathInTree(await GetForumRoot(), curForum.ForumId);
                //IEnumerable<PhpbbForums> forumAncestors = null;
                //using (var connection = _context.Database.GetDbConnection())
                //{
                //    await connection.OpenIfNeeded();
                //    forumAncestors = await connection.QueryAsync<PhpbbForums>("SELECT * FROM phpbb_forums WHERE forum_id IN @forumIds", new { forumIds = treeAncestors.Select(x => x.ForumId) });
                //}
                var usr = await GetCurrentUserAsync();
                var restrictedAncestor = (await _forumService.GetRestrictedForumList(usr))?.FirstOrDefault(f => f == forumId);
                    //forumAncestors?.FirstOrDefault(
                    //    f => !string.IsNullOrEmpty(f.ForumPassword) && (HttpContext.Session.GetInt32($"ForumLogin_{f.ForumId}") ?? 0) != 1);

                if (restrictedAncestor != null)
                {
                    if (usr?.AllPermissions?.Any(p => p.ForumId == restrictedAncestor && p.AuthRoleId == Constants.ACCESS_TO_FORUM_DENIED_ROLE) ?? false)
                    {
                        return Unauthorized();
                    }
                    else
                    {
                        return RedirectToPage("ForumLogin", new ForumLoginModel(_context)
                        {
                            ReturnUrl = HttpUtility.UrlEncode(HttpContext.Request.Path + HttpContext.Request.QueryString),
                            ForumId = restrictedAncestor.Value
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
            PhpbbTopics curTopic;
            using (var connection = _context.Database.GetDbConnection())
            {
                await connection.OpenIfNeeded();
                curTopic = await connection.QuerySingleOrDefaultAsync<PhpbbTopics>("SELECT * FROM phpbb_topics WHERE topic_id = @topicId", new { topicId });
            }
            if (curTopic == null)
            {
                return NotFound();
            }
            return await WithValidForum(curTopic.ForumId, async (curForum) => await toDo(curForum, curTopic));
        }

        protected async Task<IActionResult> WithValidPost(int postId, Func<PhpbbForums, PhpbbTopics, PhpbbPosts, Task<IActionResult>> toDo)
        {
            //var curPost = await _context.PhpbbPosts.AsNoTracking().FirstOrDefaultAsync(p => p.PostId == postId);
            PhpbbPosts curPost;
            using (var connection = _context.Database.GetDbConnection())
            {
                await connection.OpenIfNeeded();
                curPost = await connection.QuerySingleOrDefaultAsync<PhpbbPosts>("SELECT * FROM phpbb_posts WHERE post_id = @postId", new { postId });
            }
            if (curPost == null)
            {
                return NotFound();
            }
            return await WithValidTopic(curPost.TopicId, async (curForum, curTopic) => await toDo(curForum, curTopic, curPost));
        }

        #endregion Permission validation wrappers
    }
}
