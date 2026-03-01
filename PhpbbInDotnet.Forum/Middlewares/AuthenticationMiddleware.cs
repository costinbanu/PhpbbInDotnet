using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
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
using System.Threading.Tasks;

namespace PhpbbInDotnet.Forum.Middlewares
{
    public class AuthenticationMiddleware : IMiddleware
    {
        private readonly ILogger _logger;
        private readonly ISqlExecuter _sqlExecuter;
        private readonly IUserService _userService;
        private readonly IConfiguration _config;
        private readonly ISessionManager _sessionCounter;

        public AuthenticationMiddleware(ILogger logger, IConfiguration config, ISqlExecuter sqlExecuter, IUserService userService, ISessionManager sessionCounter)
        {
            _logger = logger;
            _sqlExecuter = sqlExecuter;
            _userService = userService;
            _config = config;
            _sessionCounter = sessionCounter;
        }

        public async Task InvokeAsync(HttpContext context, RequestDelegate next)
        {
            ForumUser baseUser;
            PhpbbUsers? dbUser;
            if (IdentityUtility.TryGetUserId(context.User, out var userId))
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
            context.Items[nameof(ForumUserExpanded)] = user;

            var sessionTrackingTimeout = _config.GetValue<TimeSpan?>("UserActivityTrackingInterval") ?? TimeSpan.FromHours(1);
            try
            {
                if (user.IsAnonymous && !context.Request.Cookies.IsAnonymousSessionStarted())
                {
                    if (context.IsBot())
                    {
                        _sessionCounter.UpsertBot(context.GetIpAddress() ?? "n/a", context.Request.Headers.UserAgent.ToString() ?? "n/a", sessionTrackingTimeout);
                    }
                    else
                    {
                        var sessionId = context.Response.Cookies.StartAnonymousSession();
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
