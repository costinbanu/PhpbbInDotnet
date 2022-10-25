using Microsoft.AspNetCore.Http;
using PhpbbInDotnet.Database;
using PhpbbInDotnet.Database.Entities;
using PhpbbInDotnet.Domain.Extensions;
using PhpbbInDotnet.Domain.Utilities;
using PhpbbInDotnet.Objects;
using PhpbbInDotnet.Services;
using Serilog;
using System;
using System.Threading.Tasks;

namespace PhpbbInDotnet.Forum.Middlewares
{
    public class ErrorHandlingMiddleware : IMiddleware
    {
        private readonly IForumDbContext _dbContext;
        private readonly ILogger _logger;

        public ErrorHandlingMiddleware(IForumDbContext dbContext, ILogger logger)
        {
            _dbContext = dbContext;
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
                    user = await _dbContext.GetSqlExecuter().QueryFirstOrDefaultAsync<PhpbbUsers>(
                        "SELECT * FROM phpbb_users WHERE user_id = @userId",
                        new { userId });
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
