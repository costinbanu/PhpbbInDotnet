using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using PhpbbInDotnet.Domain.Extensions;
using PhpbbInDotnet.Domain.Utilities;
using PhpbbInDotnet.Services;

namespace PhpbbInDotnet.Forum.Middlewares;

public class BotDetectorMiddleware(ISessionManager sessionManager) : IMiddleware
{
    public Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        if ((IdentityUtility.TryGetUserId(context.User, out var userId) && IdentityUtility.IsValidRegisteredUserId(userId)) ||
            sessionManager.IsSessionStarted(context.Request.Cookies.GetAnonymousSessionId()) ||
            context.Request.Path.Equals("/BotChallenge", StringComparison.InvariantCultureIgnoreCase))
        {
            return next(context);

        }

        context.Response.Redirect($"/BotChallenge?Target={Uri.EscapeDataString(context.Request.GetEncodedPathAndQuery())}");

        return Task.CompletedTask;
    }
}