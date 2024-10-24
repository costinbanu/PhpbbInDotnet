using DeviceDetectorNET;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Net.Http.Headers;
using PhpbbInDotnet.Database.Entities;
using PhpbbInDotnet.Database.SqlExecuter;
using PhpbbInDotnet.Domain;
using PhpbbInDotnet.Domain.Extensions;
using PhpbbInDotnet.Domain.Utilities;
using PhpbbInDotnet.Objects;
using PhpbbInDotnet.Services;
using Serilog;
using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace PhpbbInDotnet.Forum.Middlewares
{
    public class AuthenticationMiddleware : IMiddleware
    {
        private readonly ILogger _logger;
        private readonly ISqlExecuter _sqlExecuter;
        private readonly IUserService _userService;
        private readonly IConfiguration _config;
        private readonly IAnonymousSessionCounter _sessionCounter;

        public AuthenticationMiddleware(ILogger logger, IConfiguration config, ISqlExecuter sqlExecuter, IUserService userService, IAnonymousSessionCounter sessionCounter)
        {
            _logger = logger;
            _sqlExecuter = sqlExecuter;
            _userService = userService;
            _config = config;
            _sessionCounter = sessionCounter;
        }

        public async Task InvokeAsync(HttpContext context, RequestDelegate next)
        {
            var isBot = false;
            string? userAgent = null;
            var hasUserId = IdentityUtility.TryGetUserId(context.User, out var userId);
            if ((!hasUserId || userId == 1) && context.Request.Headers.TryGetValue(HeaderNames.UserAgent, out var header))
            {
                try
                {
                    userAgent = header.ToString();
                    var dd = new DeviceDetector(userAgent, ClientHints.Factory(context.Request.Headers.ToDictionary(a => a.Key, a => a.Value.ToArray().FirstOrDefault())));
                    dd.Parse();
                    if (dd.IsBot())
                    {
                        isBot = true;
                        var now = DateTime.UtcNow;
                        var shouldRateLimitBots = _config.GetValue<bool>("RateLimitBots");
                        var shouldLimitBasedOnSessionCount = _sessionCounter.GetActiveBotCountByUserAgent(userAgent) > 50 && !context.Request.Cookies.IsAnonymousSessionStarted();

                        if (shouldRateLimitBots && shouldLimitBasedOnSessionCount)
                        {
                            context.Response.StatusCode = (int)HttpStatusCode.TooManyRequests;
                            return;
                        }
                    }
				}
                catch (Exception ex)
                {
                    _logger.Warning(ex, "Failed to detect if session is bot. User agent: {userAgent}, IP: {ip}", userAgent, context.Connection.RemoteIpAddress);
                }
            }

            ForumUser baseUser;
            PhpbbUsers? dbUser;
            if (hasUserId)
            {
                dbUser = await _sqlExecuter.QueryFirstOrDefaultAsync<PhpbbUsers>(
                    "SELECT * FROM phpbb_users WHERE user_id = @userId",
                    new { userId });

                if (dbUser is null || dbUser.UserShouldSignIn || (dbUser.UserInactiveReason != UserInactiveReason.NotInactive && dbUser.UserInactiveReason != UserInactiveReason.Active_NotConfirmed))
                {
                    await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                    await SignInAnonymousUser(context);
                    dbUser = await _userService.GetAnonymousDbUserAsync();
                    baseUser = _userService.DbUserToForumUser(dbUser);
                }
                else
                {
                    baseUser = _userService.DbUserToForumUser(dbUser);
                }
            }
            else
            {
                await SignInAnonymousUser(context);
                dbUser = await _userService.GetAnonymousDbUserAsync();
                baseUser = _userService.DbUserToForumUser(dbUser);
            }

            var expansions = ForumUserExpansionType.Permissions;
            if (!baseUser.IsAnonymous)
            {
                expansions |= ForumUserExpansionType.TopicPostsPerPage | ForumUserExpansionType.Foes | ForumUserExpansionType.UploadLimit | ForumUserExpansionType.PostEditTime | ForumUserExpansionType.Style;
            }

            var user = await _userService.ExpandForumUser(baseUser, expansions);
            user.SetValue(context);

            var sessionTrackingTimeout = _config.GetValue<TimeSpan?>("UserActivityTrackingInterval") ?? TimeSpan.FromHours(1);
            try
            {
                if (user.IsAnonymous && !context.Request.Cookies.IsAnonymousSessionStarted())
                {
                    var sessionId = context.Response.Cookies.StartAnonymousSession();
                    if (isBot)
                    {
                        _sessionCounter.UpsertBot(context.Connection.RemoteIpAddress?.ToString() ?? "n/a", userAgent ?? "n/a", sessionTrackingTimeout);
                    }
                    else
                    {
                        _sessionCounter.UpsertSession(sessionId, sessionTrackingTimeout);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Warning(ex, "Failed to detect anonymous session type.");
            }

            await next(context);

            try
            {
                if (!user.IsAnonymous && DateTime.UtcNow.Subtract(dbUser.UserLastvisit.ToUtcTime()) > sessionTrackingTimeout)
                {
                    await _sqlExecuter.ExecuteAsyncWithoutResiliency(
                        "UPDATE phpbb_users SET user_lastvisit = @now WHERE user_id = @userId",
                        new { now = DateTime.UtcNow.ToUnixTimestamp(), user.UserId },
                        commandTimeout: 10);
                }
            }
            catch (Exception ex)
            {
                _logger.Warning(ex, "Failed to update user last visit for {username}.", user.Username);
            }
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
    }
}
