using Microsoft.AspNetCore.Http;
using PhpbbInDotnet.Services;
using PhpbbInDotnet.Utilities;
using System;
using System.Threading.Tasks;

namespace PhpbbInDotnet.Forum.Middlewares
{
    public class ErrorHandlingMiddleware : IMiddleware
    {
        private readonly IUserService _userService;
        private readonly ICommonUtils _utils;

        public ErrorHandlingMiddleware(ICommonUtils utils, IUserService userService)
        {
            _utils = utils;
            _userService = userService;
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
                var id = _utils.HandleError(ex, $"URL: {path}{context.Request?.QueryString}. UserId: {user?.UserId.ToString() ?? "N/A"}. UserName: {user?.Username ?? "N/A"}");

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
