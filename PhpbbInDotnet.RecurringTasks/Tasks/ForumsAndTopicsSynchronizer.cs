using PhpbbInDotnet.Database.SqlExecuter;
using PhpbbInDotnet.Domain;
using PhpbbInDotnet.Services.Caching;
using Serilog;
using System.Threading;
using System.Threading.Tasks;

namespace PhpbbInDotnet.RecurringTasks.Tasks
{
    class ForumsAndTopicsSynchronizer : IRecurringTask
    {
        private readonly ISqlExecuter _sqlExecuter;
		private readonly ILogger _logger;
		private readonly ICachedDbInfoService _cachedDbInfoService;

		public ForumsAndTopicsSynchronizer(ISqlExecuter sqlExecuter, ILogger logger, ICachedDbInfoService cachedDbInfoService)
        {
            _sqlExecuter = sqlExecuter;
            _logger = logger;
            _cachedDbInfoService = cachedDbInfoService;
        }

        public async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await _sqlExecuter.CallStoredProcedureAsync("sync_post_forum");

            stoppingToken.ThrowIfCancellationRequested();

            await _sqlExecuter.CallStoredProcedureAsync("sync_last_posts", new
            {
                Constants.ANONYMOUS_USER_ID,
                Constants.ANONYMOUS_USER_NAME,
                Constants.DEFAULT_USER_COLOR
            });

			stoppingToken.ThrowIfCancellationRequested();

			await _sqlExecuter.ExecuteAsync(
                @"WITH post_counts AS (
	                SELECT poster_id, count(post_id) as post_count
	                  FROM phpbb_posts
	                 GROUP BY poster_id
                )
                UPDATE u
                   SET u.user_posts = c.post_count
                   FROM phpbb_users u
                   JOIN post_counts c ON u.user_id = c.poster_id");

			stoppingToken.ThrowIfCancellationRequested();

			await _cachedDbInfoService.ForumTopicCount.InvalidateAsync();
            await _cachedDbInfoService.ForumTree.InvalidateAsync();

            _logger.Information("Successfully synced forums and topics with their last posts.");
        }
    }
}
