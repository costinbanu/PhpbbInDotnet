using Microsoft.Extensions.Configuration;
using PhpbbInDotnet.Database.Entities;
using PhpbbInDotnet.Database.SqlExecuter;
using PhpbbInDotnet.Domain;
using PhpbbInDotnet.Domain.Extensions;
using PhpbbInDotnet.Domain.Utilities;
using PhpbbInDotnet.Objects;
using PhpbbInDotnet.Services;
using PhpbbInDotnet.Services.Storage;
using Serilog;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PhpbbInDotnet.RecurringTasks.Tasks
{
    class RecycleBinCleaner : IRecurringTask
    {
        readonly IConfiguration _config;
        readonly ITimeService _timeService;
        readonly ISqlExecuter _sqlExecuter;
        readonly IStorageService _storageService;
        readonly ILogger _logger;

        public RecycleBinCleaner(IConfiguration config, ITimeService timeService, ISqlExecuter sqlExecuter, IStorageService storageService, ILogger logger)
        {
            _config = config;
            _timeService = timeService;
            _sqlExecuter = sqlExecuter;
            _storageService = storageService;
            _logger = logger;
        }

        public async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var retention = _config.GetObjectOrDefault<TimeSpan?>("RecycleBinRetentionTime") ?? TimeSpan.FromDays(7);

            if (retention < TimeSpan.FromDays(1))
            {
                throw new ArgumentOutOfRangeException("RecycleBinRetentionTime", "Invalid app setting value.");
            }

            var toDelete = await _sqlExecuter.QueryAsync<PhpbbRecycleBin>(
                "SELECT * FROM phpbb_recycle_bin WHERE @now - delete_time > @retention",
                new
                {
                    now = _timeService.DateTimeUtcNow().ToUnixTimestamp(),
                    retention = retention.TotalSeconds
                });

            if (!toDelete.Any())
            {
                _logger.Information("Nothing to delete from the recycle bin.");
                return;
            }
            else
            {
                _logger.Information("Deleting {count} items older than {retention} from the recycle bin...", toDelete.Count(), retention);
            }

            stoppingToken.ThrowIfCancellationRequested();

            await _sqlExecuter.ExecuteAsync(
                "DELETE FROM phpbb_recycle_bin WHERE type = @type AND id = @id",
                toDelete);

            var posts = await Task.WhenAll(
                from i in toDelete
                where i.Type == RecycleBinItemType.Post
                select CompressionUtility.DecompressObject<PostDto>(i.Content)
            );

            stoppingToken.ThrowIfCancellationRequested();

            var deleteTasks = from p in posts
                              where p?.Attachments?.Any() == true

                              from a in p.Attachments!
                              where !string.IsNullOrWhiteSpace(a?.PhysicalFileName)

                              select _storageService.DeleteAttachment(a!.PhysicalFileName!);

            var deleteResults = await Task.WhenAll(deleteTasks);
            if (deleteResults.Any(r => !r))
            {
                _logger.Warning("Not all attachments have been permanently deleted successfully. This could have happened due to them being already deleted.");
            }
        }
    }
}
