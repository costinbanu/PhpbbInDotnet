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
using System.Threading.Tasks;

namespace PhpbbInDotnet.Forum.Middlewares
{
    public class AuthenticationMiddleware(ILogger logger, IConfiguration config, ISqlExecuter sqlExecuter, IUserService userService, ISessionManager sessionManager) : IMiddleware
    {
        public async Task InvokeAsync(HttpContext context, RequestDelegate next)
        {
            ForumUser baseUser;
            PhpbbUsers? dbUser;
            if (IdentityUtility.TryGetUserId(context.User, out var userId) && IdentityUtility.IsValidRegisteredUserId(userId))
            {
                dbUser = await sqlExecuter.QueryFirstOrDefaultAsync<PhpbbUsers>(
                    "SELECT * FROM phpbb_users WHERE user_id = @userId",
                    new { userId });

                if (dbUser is null || dbUser.UserShouldSignIn || (dbUser.UserInactiveReason != UserInactiveReason.NotInactive && dbUser.UserInactiveReason != UserInactiveReason.Active_NotConfirmed))
                {
                    await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                    await SignInAnonymousUser(context);
                    dbUser = await userService.GetAnonymousDbUserAsync();
                    baseUser = userService.DbUserToForumUser(dbUser);
                }
                else
                {
                    baseUser = userService.DbUserToForumUser(dbUser);
                }
            }
            else
            {
                await SignInAnonymousUser(context);
                dbUser = await userService.GetAnonymousDbUserAsync();
                baseUser = userService.DbUserToForumUser(dbUser);
            }

            ForumUserExpanded user;
            if (!baseUser.IsAnonymous)
            {
                var expansions = ForumUserExpansionType.Permissions | ForumUserExpansionType.TopicPostsPerPage | ForumUserExpansionType.Foes | ForumUserExpansionType.UploadLimit | ForumUserExpansionType.PostEditTime | ForumUserExpansionType.Style;
                user = await userService.ExpandForumUser(baseUser, expansions);
            }
            else
            {
                user = await userService.GetAnonymousForumUserExpandedAsync();
            }

            context.Items[nameof(ForumUserExpanded)] = user;

            var sessionTrackingTimeout = config.GetValue<TimeSpan?>("UserActivityTrackingInterval") ?? TimeSpan.FromHours(1);
            try
            {
                if (user.IsAnonymous && !sessionManager.IsSessionStarted(context.Request.Cookies.GetAnonymousSessionId()))
                {
                    var sessionId = context.Response.Cookies.StartAnonymousSession();
                    if (context.IsBot())
                    {
                        sessionManager.UpsertBot(context.GetIpAddress() ?? "n/a", context.Request.Headers.UserAgent.ToString() ?? "n/a", sessionId, sessionTrackingTimeout);
                    }
                    else
                    {
                        sessionManager.UpsertSession(sessionId, sessionTrackingTimeout);
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Warning(ex, "Failed to detect anonymous session type.");
            }

            await next(context);

            try
            {
                if (!user.IsAnonymous && DateTime.UtcNow.Subtract(dbUser.UserLastvisit.ToUtcTime()) > sessionTrackingTimeout)
                {
                    await sqlExecuter.ExecuteAsyncWithoutResiliency(
                        "UPDATE phpbb_users SET user_lastvisit = @now WHERE user_id = @userId",
                        new { now = DateTime.UtcNow.ToUnixTimestamp(), user.UserId },
                        commandTimeout: 10);
                }
            }
            catch (Exception ex)
            {
                logger.Warning(ex, "Failed to update user last visit for {username}.", user.Username);
            }
        }

        private async Task SignInAnonymousUser(HttpContext context)
        {
            var authenticationExpiration = config.GetValue<TimeSpan?>("LoginSessionSlidingExpiration") ?? TimeSpan.FromDays(30);
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
