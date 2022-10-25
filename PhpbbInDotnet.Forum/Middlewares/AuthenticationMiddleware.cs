using DeviceDetectorNET;
using LazyCache;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Net.Http.Headers;
using PhpbbInDotnet.Database;
using PhpbbInDotnet.Database.Entities;
using PhpbbInDotnet.Domain;
using PhpbbInDotnet.Domain.Extensions;
using PhpbbInDotnet.Domain.Utilities;
using PhpbbInDotnet.Objects;
using PhpbbInDotnet.Services;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
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
        private readonly IAnonymousSessionCounter _sessionCounter;

        static AuthenticationMiddleware()
        {
            EXCLUDED_PAGES = new HashSet<string>(
                new[] { "/Login", "/Logout", "/Register" },
                StringComparer.OrdinalIgnoreCase);
        }

        public AuthenticationMiddleware(ILogger logger, IConfiguration config, IAppCache cache, IForumDbContext context,
            IForumTreeService forumTreeService, IUserService userService, IAnonymousSessionCounter sessionCounter)
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

            var sqlExecuter = _context.GetSqlExecuter();
            AuthenticatedUser baseUser;
            PhpbbUsers dbUser;
            if (IdentityUtility.TryGetUserId(context.User, out var userId))
            {
                dbUser = await sqlExecuter.QueryFirstOrDefaultAsync<PhpbbUsers>(
                    "SELECT * FROM phpbb_users WHERE user_id = @userId",
                    new { userId });

                if (dbUser is null || dbUser.UserShouldSignIn || dbUser.UserInactiveReason != UserInactiveReason.NotInactive)
                {
                    await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                    await SignInAnonymousUser(context);
                    dbUser = await _userService.GetAnonymousDbUser();
                    baseUser = _userService.DbUserToAuthenticatedUserBase(dbUser);
                }
                else
                {
                    baseUser = _userService.DbUserToAuthenticatedUserBase(dbUser);
                }
            }
            else
            {
                await SignInAnonymousUser(context);
                dbUser = await _userService.GetAnonymousDbUser();
                baseUser = _userService.DbUserToAuthenticatedUserBase(dbUser);
            }

            var sessionTrackingTimeout = _config.GetValue<TimeSpan?>("UserActivityTrackingInterval") ?? TimeSpan.FromHours(1);
            var permissionsTask = _userService.GetPermissions(baseUser.UserId);
            var user = new AuthenticatedUserExpanded(baseUser);
            if (!baseUser.IsAnonymous)
            {
                var tppTask = GetTopicPostsPage(baseUser.UserId);
                var foesTask = _userService.GetFoes(baseUser.UserId);
                var groupPropertiesTask = sqlExecuter.QueryFirstOrDefaultAsync<(int GroupEditTime, int GroupUserUploadSize)>(
                    @"SELECT g.group_edit_time, g.group_user_upload_size
                        FROM phpbb_groups g
                        JOIN phpbb_users u ON g.group_id = u.group_id
                       WHERE u.user_id = @UserId",
                    new { baseUser.UserId });
                var styleTask = sqlExecuter.QueryFirstOrDefaultAsync<string>(
                    "SELECT style_name FROM phpbb_styles WHERE style_id = @UserStyle",
                    new { dbUser.UserStyle });
                var updateLastVisitTask = DateTime.UtcNow.Subtract(dbUser.UserLastvisit.ToUtcTime()) <= sessionTrackingTimeout
                    ? Task.CompletedTask
                    : sqlExecuter.ExecuteAsync(
                        "UPDATE phpbb_users SET user_lastvisit = @now WHERE user_id = @userId",
                        new { now = DateTime.UtcNow.ToUnixTimestamp(), baseUser.UserId });

                await Task.WhenAll(styleTask, groupPropertiesTask, permissionsTask, tppTask, foesTask, updateLastVisitTask);

                var groupProperties = await groupPropertiesTask;
                var style = await styleTask;

                user.TopicPostsPerPage = await tppTask;
                user.Foes = await foesTask;
                user.UploadLimit = groupProperties.GroupUserUploadSize;
                user.PostEditTime = (groupProperties.GroupEditTime == 0 || dbUser.UserEditTime == 0) ? 0 : Math.Min(Math.Abs(groupProperties.GroupEditTime), Math.Abs(dbUser.UserEditTime));
                user.Style = style ?? string.Empty;
            }
            user.AllPermissions = await permissionsTask;

            user.SetValue(context);

            if (user.IsAnonymous && context.Request.Headers.TryGetValue(HeaderNames.UserAgent, out var header) && (context.Session.GetInt32("SessionCounted") ?? 0) == 0)
            {
                try
                {
                    var userAgent = header.ToString();
                    var dd = new DeviceDetector(userAgent);
                    dd.Parse();
                    if (dd.IsBot())
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

        private async Task SignInAnonymousUser(HttpContext context)
        {
            var authenticationExpiration = _config.GetValue<TimeSpan?>("LoginSessionSlidingExpiration") ?? TimeSpan.FromDays(30);
            await context.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                IdentityUtility.CreateClaimsPrincipal(Constants.ANONYMOUS_USER_ID),
                new AuthenticationProperties
                {
                    AllowRefresh = true,
                    ExpiresUtc = DateTimeOffset.UtcNow.Add(authenticationExpiration),
                    IsPersistent = true,
                }); 
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
    }
}
