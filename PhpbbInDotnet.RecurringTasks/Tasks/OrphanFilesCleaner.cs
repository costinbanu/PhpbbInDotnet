using Microsoft.Extensions.Configuration;
using PhpbbInDotnet.Database;
using PhpbbInDotnet.Domain.Extensions;
using PhpbbInDotnet.Services;
using Serilog;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace PhpbbInDotnet.RecurringTasks.Tasks
{
    class OrphanFilesCleaner : IRecurringTask
    {

        readonly IConfiguration _config;
        readonly ITimeService _timeService;
        readonly IForumDbContext _dbContext;
        readonly IWritingToolsService _writingToolsService;
        readonly ILogger _logger;

        public OrphanFilesCleaner(IConfiguration config, ITimeService timeService, IForumDbContext dbContext, IWritingToolsService writingToolsService, ILogger logger)
        {
            _config = config;
            _timeService = timeService;
            _dbContext = dbContext;
            _writingToolsService = writingToolsService;
            _logger = logger;
        }

        public async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var retention = _config.GetObject<TimeSpan?>("RecycleBinRetentionTime") ?? TimeSpan.FromDays(7);
            var sqlExecuter = _dbContext.GetSqlExecuter();

            stoppingToken.ThrowIfCancellationRequested();

            await sqlExecuter.ExecuteAsync(
                @"UPDATE phpbb_attachments a
                    LEFT JOIN phpbb_posts p ON a.post_msg_id = p.post_id
                     SET a.is_orphan = 1
                   WHERE p.post_id IS NULL AND @now - a.filetime > @retention AND a.is_orphan = 0",
                new
                {
                    now = _timeService.DateTimeUtcNow().ToUnixTimestamp(),
                    retention = retention.TotalSeconds
                });

            stoppingToken.ThrowIfCancellationRequested();

            var (Message, IsSuccess) = await _writingToolsService.DeleteOrphanedFiles();
            if (!(IsSuccess ?? false) && !string.IsNullOrWhiteSpace(Message))
            {
                _logger.Warning(Message);
            }
        }
    }
}

