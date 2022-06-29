using Microsoft.AspNetCore.Http;
using PhpbbInDotnet.Domain.Extensions;
using PhpbbInDotnet.Services;
using Serilog;
using System;
using System.Threading.Tasks;

namespace PhpbbInDotnet.Forum.Middlewares
{
    public class ErrorHandlingMiddleware : IMiddleware
    {
        private readonly IUserService _userService;
        private readonly ILogger _logger;

        public ErrorHandlingMiddleware(IUserService userService, ILogger logger)
        {
            _userService = userService;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context, RequestDelegate next)
        {
            try
            {
                await next(context);
            }
            catch (Exception ex)
            {
                var user = _userService?.ClaimsPrincipalToAuthenticatedUser(context.User);
                var path = context.Request?.Path.Value ?? "N/A";
                var id = _logger.ErrorWithId(ex, "URL: {path}{query}. UserId: {id}, UserName: {name}", path, context.Request?.QueryString.ToString() ?? string.Empty, user?.UserId.ToString() ?? "N/A", user?.Username ?? "N/A");

                if (!path.Equals("/Error", StringComparison.InvariantCultureIgnoreCase))
                {
                    context.Response.Redirect($"/Error?errorId={id}");
                }
                else
                {
                    await context.Response.WriteAsync($"An error occurred. ID: {id}");
                }
            }
        }
    }
}
