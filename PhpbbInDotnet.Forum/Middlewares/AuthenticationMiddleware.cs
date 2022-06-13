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
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace PhpbbInDotnet.Forum.Middlewares
{
    public class AuthenticationMiddleware : IMiddleware
    {
        static readonly HashSet<string> EXCLUDED_PAGES;

        private readonly ILogger _logger;
        private readonly IForumTreeService _forumTreeService;
        private readonly IForumDbContext _context;
        private readonly IUserService _userService;
        private readonly IConfiguration _config;
        private readonly IAppCache _cache;
        private readonly AnonymousSessionCounter _sessionCounter;

        static AuthenticationMiddleware()
        {
            EXCLUDED_PAGES = new HashSet<string>(
                new[] { "/Login", "/Logout", "/Register" },
                StringComparer.InvariantCultureIgnoreCase
            );
        }

        public AuthenticationMiddleware(ILogger logger, IConfiguration config, IAppCache cache, IForumDbContext context,
            IForumTreeService forumTreeService, IUserService userService, AnonymousSessionCounter sessionCounter)
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

            AuthenticatedUserExpanded? user;
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

            var (isAllowed, dbUser) = await GetDbUserOrAnonymousIfNotAllowed(user);
            if (!isAllowed)
            {
                await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                user = _userService.ClaimsPrincipalToAuthenticatedUser(await SignInAnonymousUser(context));
            }

            var permissionsTask = _userService.GetPermissions(user!.UserId);
            var tppTask = GetTopicPostsPage(user.UserId);
            var foesTask = _userService.GetFoes(user.UserId);
            await Task.WhenAll(permissionsTask, tppTask, foesTask);

            if (dbUser is null)
            {
                var anonymousClaimsPrincipal = await SignInAnonymousUser(context);
                user = _userService.ClaimsPrincipalToAuthenticatedUser(anonymousClaimsPrincipal);
            }

            var sessionTrackingTimeout = _config.GetValue<TimeSpan?>("UserActivityTrackingInterval") ?? TimeSpan.FromHours(1);
            if (dbUser is not null && !user!.IsAnonymous && (
                    await _cache.GetAsync<bool?>($"ReloadUser_{user.UsernameClean}") == true ||
                    DateTime.UtcNow.Subtract(dbUser.UserLastvisit.ToUtcTime()) > sessionTrackingTimeout))
            {
                var claimsPrincipal = await _userService.DbUserToClaimsPrincipal(dbUser);
                await Task.WhenAll(
                    SignIn(context, claimsPrincipal),
                    _context.GetSqlExecuter().ExecuteAsync(
                        "UPDATE phpbb_users SET user_lastvisit = @now WHERE user_id = @userId",
                        new { now = DateTime.UtcNow.ToUnixTimestamp(), user.UserId }
                    )
                );
                user = _userService.ClaimsPrincipalToAuthenticatedUser(claimsPrincipal);
            }

            user!.AllPermissions = await permissionsTask;
            user.TopicPostsPerPage = await tppTask;
            user.Foes = await foesTask;

            context.Items[nameof(AuthenticatedUserExpanded)] = user;

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
                        if (context.Connection.RemoteIpAddress is not null)
                        {
                            _sessionCounter.UpsertBot(context.Connection.RemoteIpAddress.ToString(), userAgent, sessionTrackingTimeout);
                        }
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

        async Task<Dictionary<int, int>> GetTopicPostsPage(int userId)
        {
            var results = await _context.GetSqlExecuter().QueryAsync(
                @"SELECT topic_id, post_no
	                FROM phpbb_user_topic_post_number
	               WHERE user_id = @user_id
	               GROUP BY topic_id;",
                new { userId });
            return results.ToDictionary(x => checked((int)x.topic_id), y => checked((int)y.post_no));
        }

        async Task<(bool isAllowed, PhpbbUsers dbUser)> GetDbUserOrAnonymousIfNotAllowed(AuthenticatedUser user)
        {
            if (await _cache.GetAsync<bool>($"UserMustLogIn_{user.UsernameClean}"))
            {
                return (false, await _userService.GetAnonymousDbUser());
            }

            var dbUser = _context.GetSqlExecuter().QueryFirstOrDefault<PhpbbUsers>("SELECT * FROM phpbb_users where user_id = @userId", new { user.UserId });
            if (dbUser is not null && dbUser.UserInactiveReason == UserInactiveReason.NotInactive)
            {
                return (true, dbUser);
            }

            return (false, await _userService.GetAnonymousDbUser());
        }
    }
}
