using PhpbbInDotnet.Database.SqlExecuter;
using PhpbbInDotnet.Domain;
using System.Threading;
using System.Threading.Tasks;

namespace PhpbbInDotnet.RecurringTasks.Tasks
{
    class ForumsAndTopicsSynchronizer : IRecurringTask
    {
        readonly ISqlExecuter _sqlExecuter;

        public ForumsAndTopicsSynchronizer(ISqlExecuter sqlExecuter)
        {
            _sqlExecuter = sqlExecuter;
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
        }
    }
}
