using System;
using System.Net;
using System.Threading.Tasks;
using System.Web;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using PhpbbInDotnet.Domain.Extensions;
using PhpbbInDotnet.Domain.Utilities;
using PhpbbInDotnet.Objects.Configuration;
using PhpbbInDotnet.Services;

namespace PhpbbInDotnet.Forum.Middlewares;

public class RateLimitingMiddleware(IConfiguration configuration, IAnonymousSessionCounter anonymousSessionCounter, IDistributedCache cache) : IMiddleware
{
    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        var userAgent = context.Request.Headers.UserAgent.ToString();
        var ip = context.GetIpAddress() ?? "n/a";
        var sessionId = IdentityUtility.TryGetUserId(context.User, out var userId) ? userId.ToString() : context.Request.Cookies.GetAnonymousSessionId();
        var options = configuration.GetObject<RateLimitOptions>();

        if (options.ShouldRateLimit && await ShouldRateLimitPage(context) && anonymousSessionCounter.ShouldRateLimit(userAgent, ip, sessionId, options.Threshold, options.TimeWindow))
        {
            context.Response.StatusCode = (int)HttpStatusCode.TooManyRequests;
            return;
        }

        await next(context);
    }

    private async Task<bool> ShouldRateLimitPage(HttpContext context)
    {
        var path = context.Request.Path.Value;
        var referrer = context.Request.Headers.Referer.ToString();

        if(path?.Equals("/file", StringComparison.OrdinalIgnoreCase) == true)
        {
            if (Uri.TryCreate(referrer, UriKind.Absolute, out var referrerUri) && referrerUri.Host.Equals(context.Request.Host.Host, StringComparison.OrdinalIgnoreCase))
            {
                var queryString = HttpUtility.ParseQueryString(context.Request.QueryString.Value ?? string.Empty);
                if (queryString["handler"] == "avatar" && int.TryParse(queryString["userId"], out var userId))
                {
                    var avatar = await cache.GetStringAsync(CacheUtility.GetAvatarCacheKey(userId));
                    if (!string.IsNullOrEmpty(avatar))
                    {
                        return false;
                    }
                }
                else if (int.TryParse(queryString["id"], out var attachId) && int.TryParse(queryString["postId"], out var postId))
                {
                    var attachment = await cache.GetAsync(CacheUtility.GetAttachmentCacheKey(attachId, postId));
                    if (attachment?.Length > 0)
                    {
                        return false;
                    }
                }
            }
        }       
        return true;
    }
}
