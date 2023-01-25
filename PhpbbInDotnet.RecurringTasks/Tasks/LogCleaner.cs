using Microsoft.Extensions.Configuration;
using PhpbbInDotnet.Database;
using PhpbbInDotnet.Database.Entities;
using PhpbbInDotnet.Domain.Extensions;
using PhpbbInDotnet.Services;
using Serilog;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PhpbbInDotnet.RecurringTasks.Tasks
{
    class LogCleaner : IRecurringTask
    {
        readonly IConfiguration _config;
        readonly ITimeService _timeService;
        readonly ISqlExecuter _sqlExecuter;
        readonly ILogger _logger;

        public LogCleaner(IConfiguration config, ITimeService timeService, ISqlExecuter sqlExecuter, ILogger logger)
        {
            _config = config;
            _timeService = timeService;
            _sqlExecuter = sqlExecuter;
            _logger = logger;
        }

        public async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var retention = _config.GetObject<TimeSpan?>("OperationLogsRetentionTime") ?? TimeSpan.FromDays(365);

            if (retention == TimeSpan.Zero)
            {
                _logger.Information("Was instructed to keep operation logs indefinitely, will not delete.");
                return;
            }

            if (retention < TimeSpan.FromDays(1))
            {
                throw new ArgumentOutOfRangeException("OperationLogsRetentionTime", "Invalid app setting value.");
            }

            var toDelete = await _sqlExecuter.QueryAsync<PhpbbLog>(
                "SELECT * FROM phpbb_log WHERE @now - log_time > @retention",
                new
                {
                    now = _timeService.DateTimeUtcNow().ToUnixTimestamp(),
                    retention = retention.TotalSeconds
                });

            if (!toDelete.Any())
            {
                _logger.Information("Nothing to delete from the operation logs.");
                return;
            }
            else
            {
                _logger.Information("Deleting {count} items older than {retention} from the operation logs...", toDelete.Count(), retention);
            }

            stoppingToken.ThrowIfCancellationRequested();

            await _sqlExecuter.ExecuteAsync(
                "DELETE FROM phpbb_log WHERE log_id IN @ids",
                new
                {
                    ids = toDelete.Select(l => l.LogId).DefaultIfEmpty()
                });
        }
    }
}
