using System;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace PhpbbInDotnet.Forum.Middlewares;

public class HostCheckerMiddleware(IConfiguration config) : IMiddleware
{
    public Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        var allowedHost = new Uri(config.GetValue<string>("BaseUrl")!).Host;
        if (!context.Request.Host.Host.Equals(allowedHost, StringComparison.InvariantCultureIgnoreCase))
        {
            context.Response.StatusCode = (int)HttpStatusCode.Forbidden;
            return Task.CompletedTask;
        }

        return next(context);
    }
}
