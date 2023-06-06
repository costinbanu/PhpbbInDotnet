﻿using DeviceDetectorNET;
using LazyCache;
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
using System.Collections.Generic;
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
            ForumUser baseUser;
            PhpbbUsers dbUser;
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

            var sessionTrackingTimeout = _config.GetValue<TimeSpan?>("UserActivityTrackingInterval") ?? TimeSpan.FromHours(1);
            var updateLastVisitTask = Task.CompletedTask;
            var expansions = ForumUserExpansionType.Permissions;
            if (!baseUser.IsAnonymous)
            {
                expansions |= ForumUserExpansionType.TopicPostsPerPage | ForumUserExpansionType.Foes | ForumUserExpansionType.UploadLimit | ForumUserExpansionType.PostEditTime | ForumUserExpansionType.Style;
                if (DateTime.UtcNow.Subtract(dbUser.UserLastvisit.ToUtcTime()) > sessionTrackingTimeout)
                {
                    updateLastVisitTask = _sqlExecuter.ExecuteAsync(
                        "UPDATE phpbb_users SET user_lastvisit = @now WHERE user_id = @userId",
                        new { now = DateTime.UtcNow.ToUnixTimestamp(), baseUser.UserId });
                }
            }
            var userTask = _userService.ExpandForumUser(baseUser, expansions);

            await Task.WhenAll(userTask, updateLastVisitTask);

            var user = await userTask;
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
            var results = await _sqlExecuter.QueryAsync<(int topicId, int postNo)>(
                @"SELECT DISTINCT topic_id, post_no
	                FROM phpbb_user_topic_post_number
	               WHERE user_id = @user_id
                   ORDER BY topic_id;",
                new { userId });
            return results.ToDictionary(x => x.topicId, y => y.postNo);
        }
    }
}
