using Dapper;
using DeviceDetectorNET;
using LazyCache;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Net.Http.Headers;
using PhpbbInDotnet.Database;
using PhpbbInDotnet.Database.Entities;
using PhpbbInDotnet.Objects;
using PhpbbInDotnet.Services;
using PhpbbInDotnet.Utilities;
using Serilog;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace PhpbbInDotnet.Forum
{
    public class AuthenticationMiddleware : IMiddleware
    {
        static readonly HashSet<string> EXCLUDED_PAGES;
        static readonly HashSet<string> PAGES_REQUIRING_UNREAD_DATA;

        private readonly ILogger _logger;
        private readonly ForumTreeService _forumTreeService;
        private readonly ForumDbContext _context;
        private readonly UserService _userService;
        private readonly IConfiguration _config;
        private readonly IAppCache _cache;
        private readonly AnonymousSessionCounter _sessionCounter;

        static AuthenticationMiddleware()
        {
            EXCLUDED_PAGES = new HashSet<string>(
                new[] { "/Login", "/Logout", "/Register" },
                StringComparer.InvariantCultureIgnoreCase
            );
            PAGES_REQUIRING_UNREAD_DATA = new HashSet<string>(
                new[] { "/", "/Index", "/ViewForum", "/ViewTopic" },
                StringComparer.InvariantCultureIgnoreCase
            );
        }

        public AuthenticationMiddleware(ILogger logger, IConfiguration config, IAppCache cache, ForumDbContext context,
            ForumTreeService forumTreeService, UserService userService, AnonymousSessionCounter sessionCounter)
        {
            _logger = logger;
            _forumTreeService = forumTreeService;
            _context = context;
            _userService = userService;
            _config = config;
            _cache = cache;
            _sessionCounter = sessionCounter;
        }

        public async Task InvokeAsync(HttpContext context, RequestDelegate next)
        {
            if (EXCLUDED_PAGES.Contains(context.Request.Path))
            {
                await next(context);
                return;
            }

            AuthenticatedUser user;
            try
            {
                if (context.User?.Identity?.IsAuthenticated != true)
                {
                    user = _userService.ClaimsPrincipalToAuthenticatedUser(await SignInAnonymousUser(context));
                }
                else
                {
                    user = _userService.ClaimsPrincipalToAuthenticatedUser(context.User);
                }
                if (user is null)
                {
                    _logger.Warning("Failed to log in neither a proper user nor the anonymous idendity.");
                    await next(context);
                    return;
                }
            }
            catch (Exception ex)
            {
                _logger.Warning(ex, "Failed to parse claims");
                await next(context);
                return;
            }

            if (await _cache.GetAsync<bool?>($"UserMustLogIn_{user.UsernameClean}") == true)
            {
                await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                user = _userService.ClaimsPrincipalToAuthenticatedUser(await SignInAnonymousUser(context));
            }

            var connection = await _context.GetDbConnectionAsync();

            var dbUserTask = connection.QueryFirstOrDefaultAsync<PhpbbUsers>("SELECT * FROM phpbb_users WHERE user_id = @userId", new { user.UserId });
            var permissionsTask = _userService.GetPermissions(user.UserId);
            var tppTask = GetTopicPostsPage(connection, user.UserId);
            var foesTask = _userService.GetFoes(user.UserId);
            await Task.WhenAll(permissionsTask, tppTask, foesTask);
            var dbUser = await dbUserTask;

            if (dbUser == null)
            {
                var anonymousClaimsPrincipal = await SignInAnonymousUser(context);
                user = _userService.ClaimsPrincipalToAuthenticatedUser(anonymousClaimsPrincipal);
            }

            var sessionTrackingTimeout = _config.GetValue<TimeSpan?>("UserActivityTrackingInterval") ?? TimeSpan.FromHours(1);
            if (dbUser != null && !user.IsAnonymous && (
                    await _cache.GetAsync<bool?>($"ReloadUser_{user.UsernameClean}") == true ||
                    DateTime.UtcNow.Subtract(dbUser.UserLastvisit.ToUtcTime()) > sessionTrackingTimeout))
            {
                var claimsPrincipal = await _userService.DbUserToClaimsPrincipal(dbUser);
                await Task.WhenAll(
                    SignIn(context, claimsPrincipal),
                    connection.ExecuteAsync(
                        "UPDATE phpbb_users SET user_lastvisit = @now WHERE user_id = @userId",
                        new { now = DateTime.UtcNow.ToUnixTimestamp(), user.UserId }
                    ),
                    PAGES_REQUIRING_UNREAD_DATA.Contains(context.Request.Path) ? _forumTreeService.GetForumTracking(user.UserId, false) : Task.CompletedTask
                );
                user = _userService.ClaimsPrincipalToAuthenticatedUser(claimsPrincipal);
            }

            user.AllPermissions = await permissionsTask;
            user.TopicPostsPerPage = await tppTask;
            user.Foes = await foesTask;

            context.Items.TryAdd(nameof(AuthenticatedUser), user);

            if (user.IsAnonymous && context.Request.Headers.TryGetValue(HeaderNames.UserAgent, out var header) && (context.Session.GetInt32("SessionCounted") ?? 0) == 0)
            {
                try
                {
                    var userAgent = header.ToString();
                    var dd = new DeviceDetector(userAgent);
                    dd.Parse();
                    var IsBot = dd.IsBot();
                    if (IsBot)
                    {
                        _sessionCounter.UpsertBot(context.Connection.RemoteIpAddress.ToString(), userAgent, sessionTrackingTimeout);
                    }
                    else
                    {
                        context.Session.SetInt32("SessionCounted", 1);
                        _sessionCounter.UpsertSession(context.Session.Id, sessionTrackingTimeout);
                    }
                }
                catch (Exception ex)
                {
                    _logger.Warning(ex, "Failed to detect anonymous session type.");
                }
            }

            await next(context);

            static async Task<Dictionary<int, int>> GetTopicPostsPage(DbConnection conn, int userId)
                => (await conn.QueryAsync(
                        @"SELECT topic_id, post_no
	                        FROM phpbb_user_topic_post_number
	                       WHERE user_id = @user_id
	                       GROUP BY topic_id;",
                        new { userId }
                    )).ToDictionary(x => checked((int)x.topic_id), y => checked((int)y.post_no));
        }

        private async Task<ClaimsPrincipal> SignInAnonymousUser(HttpContext context)
        {
            var anonymousClaimsPrincipal = await _userService.GetAnonymousClaimsPrincipal();
            await SignIn(context, anonymousClaimsPrincipal);
            return anonymousClaimsPrincipal;
        }

        private async Task SignIn(HttpContext context, ClaimsPrincipal claimsPrincipal)
        {
            var authenticationExpiration = _config.GetValue<TimeSpan?>("LoginSessionSlidingExpiration") ?? TimeSpan.FromDays(30);
            await context.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                claimsPrincipal,
                new AuthenticationProperties
                {
                    AllowRefresh = true,
                    ExpiresUtc = DateTimeOffset.UtcNow.Add(authenticationExpiration),
                    IsPersistent = true,
                }
            );
        }
    }
}
