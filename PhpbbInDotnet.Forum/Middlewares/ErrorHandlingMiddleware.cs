using Microsoft.AspNetCore.Http;
using PhpbbInDotnet.Database.Entities;
using PhpbbInDotnet.Database.SqlExecuter;
using PhpbbInDotnet.Domain.Extensions;
using PhpbbInDotnet.Domain.Utilities;
using Serilog;
using System;
using System.Threading.Tasks;

namespace PhpbbInDotnet.Forum.Middlewares
{
    public class ErrorHandlingMiddleware : IMiddleware
    {
        private readonly ISqlExecuter _sqlExecuter;
        private readonly ILogger _logger;

        public ErrorHandlingMiddleware(ISqlExecuter sqlExecuter, ILogger logger)
        {
            _sqlExecuter = sqlExecuter;
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
                PhpbbUsers? user = null;
                if (IdentityUtility.TryGetUserId(context.User, out var userId))
                {
                    try
                    {
                        user = await _sqlExecuter.QueryFirstOrDefaultAsync<PhpbbUsers>(
                            "SELECT * FROM phpbb_users WHERE user_id = @userId",
                            new { userId });
                    }
                    catch (Exception ex1)
                    {
                        _logger.Error(ex1);
                    }
                }
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
