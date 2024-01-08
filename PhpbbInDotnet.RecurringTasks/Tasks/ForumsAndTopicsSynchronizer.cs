using PhpbbInDotnet.Database.SqlExecuter;
using PhpbbInDotnet.Domain;
using Serilog;
using System.Threading;
using System.Threading.Tasks;

namespace PhpbbInDotnet.RecurringTasks.Tasks
{
    class ForumsAndTopicsSynchronizer : IRecurringTask
    {
        readonly ISqlExecuter _sqlExecuter;
		private readonly ILogger _logger;

		public ForumsAndTopicsSynchronizer(ISqlExecuter sqlExecuter, ILogger logger)
        {
            _sqlExecuter = sqlExecuter;
            _logger = logger;
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

            _logger.Information("Successfully synced forums and topics with their last posts.");
        }
    }
}
