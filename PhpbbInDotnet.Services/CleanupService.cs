using Dapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PhpbbInDotnet.Database;
using PhpbbInDotnet.Database.Entities;
using PhpbbInDotnet.Objects;
using PhpbbInDotnet.Objects.Configuration;
using PhpbbInDotnet.Utilities;
using Serilog;
using System;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PhpbbInDotnet.Services
{
    class CleanupService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;

        public const string OK_FILE_NAME = $"{nameof(CleanupService)}.ok";

        public CleanupService(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            //hacky hack https://github.com/dotnet/runtime/issues/36063
            await Task.Yield();

            using var scope = _serviceProvider.CreateScope();
            var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
            var dbContext = scope.ServiceProvider.GetRequiredService<IForumDbContext>();
            var utils = scope.ServiceProvider.GetRequiredService<ICommonUtils>();
            var storageService = scope.ServiceProvider.GetRequiredService<IStorageService>();
            var logger = scope.ServiceProvider.GetRequiredService<ILogger>();
            var writingToolsService = scope.ServiceProvider.GetRequiredService<IWritingToolsService>();
            var timeService = scope.ServiceProvider.GetRequiredService<ITimeService>();
            var fileInfoService = scope.ServiceProvider.GetRequiredService<IFileInfoService>();

            logger.Information("Launching a new {name} instance...", nameof(CleanupService));

            var options = config.GetObject<CleanupServiceOptions>("CleanupService");
            var timeToWait = GetTimeToWaitUntilRunIsAllowed(timeService, fileInfoService, options);
            if (timeToWait > TimeSpan.Zero)
            {
                logger.Warning("Waiting for {time} before executing cleanup task...", timeToWait);
                stoppingToken.WaitHandle.WaitOne(timeToWait);
            }

            try
            {
                stoppingToken.ThrowIfCancellationRequested();

                logger.Information("Executing cleanup tasks NOW...");

                var sqlExecuter = dbContext.GetSqlExecuter();
                await Task.WhenAll(
                    CleanRecycleBin(config, sqlExecuter, utils, storageService, logger, stoppingToken),
                    ResyncOrphanFiles(config, sqlExecuter, stoppingToken),
                    ResyncForumsAndTopics(sqlExecuter, stoppingToken),
                    CleanOperationLogs(config, sqlExecuter, logger, stoppingToken)
                );

                stoppingToken.ThrowIfCancellationRequested();

                var (Message, IsSuccess) = await writingToolsService.DeleteOrphanedFiles();
                if (!(IsSuccess ?? false) && !string.IsNullOrWhiteSpace(Message))
                {
                    logger.Warning(Message);
                }

                File.WriteAllText(OK_FILE_NAME, string.Empty);
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Failed at least one cleanup task. Application will continue.");
            }
        }

        private async Task CleanRecycleBin(IConfiguration config, ISqlExecuter dbConnection, ICommonUtils utils, IStorageService storageService, ILogger logger, CancellationToken stoppingToken)
        {
            var retention = config.GetObject<TimeSpan?>("RecycleBinRetentionTime") ?? TimeSpan.FromDays(7);

            if (retention < TimeSpan.FromDays(1))
            {
                throw new ArgumentOutOfRangeException("RecycleBinRetentionTime", "Invalid app setting value.");
            }

            var now = DateTime.UtcNow.ToUnixTimestamp();
            var toDelete = await dbConnection.QueryAsync<PhpbbRecycleBin>(
                "SELECT * FROM phpbb_recycle_bin WHERE @now - delete_time > @retention",
                new
                {
                    now = DateTime.UtcNow.ToUnixTimestamp(),
                    retention = retention.TotalSeconds
                });

            if (!toDelete.Any())
            {
                logger.Information("Nothing to delete from the recycle bin.");
                return;
            }
            else
            {
                logger.Information("Deleting {count} items older than {retention} from the recycle bin...", toDelete.Count(), retention);
            }

            stoppingToken.ThrowIfCancellationRequested();

            await dbConnection.ExecuteAsync(
                "DELETE FROM phpbb_recycle_bin WHERE type = @type AND id = @id",
                toDelete);

            var posts = await Task.WhenAll(
                from i in toDelete
                where i.Type == RecycleBinItemType.Post
                select utils.DecompressObject<PostDto>(i.Content)
            );

            stoppingToken.ThrowIfCancellationRequested();

            var deleteResults = from p in posts
                                where p?.Attachments?.Any() == true

                                from a in p.Attachments!
                                where !string.IsNullOrWhiteSpace(a?.PhysicalFileName)

                                select storageService.DeleteFile(a!.PhysicalFileName, false);

            if (deleteResults.Any(r => !r))
            {
                logger.Warning("Not all attachments have been permanently deleted successfully. This could have happened due to them being already deleted.");
            }
        }

        private async Task ResyncOrphanFiles(IConfiguration config, ISqlExecuter dbConnection, CancellationToken stoppingToken)
        {
            var retention = config.GetObject<TimeSpan?>("RecycleBinRetentionTime") ?? TimeSpan.FromDays(7);

            stoppingToken.ThrowIfCancellationRequested();

            await dbConnection.ExecuteAsync(
                @"UPDATE phpbb_attachments a
                    LEFT JOIN phpbb_posts p ON a.post_msg_id = p.post_id
                     SET a.is_orphan = 1
                   WHERE p.post_id IS NULL AND @now - a.filetime > @retention AND a.is_orphan = 0",
                new
                {
                    now = DateTime.UtcNow.ToUnixTimestamp(),
                    retention = retention.TotalSeconds
                });
        }

        private async Task ResyncForumsAndTopics(ISqlExecuter dbConnection, CancellationToken stoppingToken)
        {
            stoppingToken.ThrowIfCancellationRequested();

            var postsHavingWrongForumIdTask = dbConnection.ExecuteAsync(
                @"UPDATE phpbb_posts p
                    JOIN phpbb_topics t ON p.topic_id = t.topic_id
                     SET p.forum_id = t.forum_id
                   WHERE p.forum_id <> t.forum_id");

            var forumsHavingWrongLastPostTask = dbConnection.ExecuteAsync(
                @"UPDATE phpbb_forums f
                    JOIN (
                        WITH maxes AS (
	                        SELECT forum_id, MAX(post_time) AS post_time
	                         FROM phpbb_posts
	                        GROUP BY forum_id
					    )
                        SELECT DISTINCT p.*
						  FROM phpbb_posts p
						  JOIN maxes m ON p.forum_id = m.forum_id AND p.post_time = m.post_time
					) lp ON f.forum_id = lp.forum_id
                  LEFT JOIN phpbb_users u ON lp.poster_id = u.user_id
                   SET f.forum_last_post_id = lp.post_id,
	                   f.forum_last_poster_id = COALESCE(u.user_id, @ANONYMOUS_USER_ID),
                       f.forum_last_post_subject = lp.post_subject,
                       f.forum_last_post_time = lp.post_time,
                       f.forum_last_poster_name = COALESCE(u.username, @ANONYMOUS_USER_NAME),
                       f.forum_last_poster_colour = COALESCE(u.user_colour, @DEFAULT_USER_COLOR)
                 WHERE lp.post_id <> f.forum_last_post_id", 
                new 
                {
                    Constants.ANONYMOUS_USER_ID,
                    Constants.ANONYMOUS_USER_NAME,
                    Constants.DEFAULT_USER_COLOR
                });

            var topicsHavingWrongLastOrFirstPostTask = dbConnection.ExecuteAsync(
                @"UPDATE phpbb_topics t
                    JOIN (
                        WITH maxes AS (
	                      SELECT topic_id, MAX(post_time) AS post_time
		                    FROM phpbb_posts
		                   GROUP BY topic_id
                        )
	                    SELECT DISTINCT p.*
	                        FROM phpbb_posts p
	                        JOIN maxes m ON p.topic_id = m.topic_id AND p.post_time = m.post_time
                    ) lp ON t.topic_id = lp.topic_id
                    JOIN (
                        WITH mins AS (
	                        SELECT topic_id, MIN(post_time) AS post_time
	                          FROM phpbb_posts
	                         GROUP BY topic_id
                        )
	                    SELECT DISTINCT p.*
	                        FROM phpbb_posts p
	                        JOIN mins m ON p.topic_id = m.topic_id AND p.post_time = m.post_time
                    ) fp ON t.topic_id = fp.topic_id
                  LEFT JOIN phpbb_users lpu ON lp.poster_id = lpu.user_id
                  LEFT JOIN phpbb_users fpu ON fp.poster_id = fpu.user_id
                   SET t.topic_last_post_id = lp.post_id,
	                   t.topic_last_poster_id = COALESCE(lpu.user_id, @ANONYMOUS_USER_ID),
                       t.topic_last_post_subject = lp.post_subject,
                       t.topic_last_post_time = lp.post_time,
                       t.topic_last_poster_name = COALESCE(lpu.username, @ANONYMOUS_USER_NAME),
                       t.topic_last_poster_colour = COALESCE(lpu.user_colour, @DEFAULT_USER_COLOR),
                       t.topic_first_post_id = fp.post_id,
                       t.topic_first_poster_name = COALESCE(fpu.username, @ANONYMOUS_USER_NAME),
                       t.topic_first_poster_colour = COALESCE(fpu.user_colour, @DEFAULT_USER_COLOR)
                 WHERE lp.post_id <> t.topic_last_post_id OR fp.post_id <> t.topic_first_post_id",
                new
                {
                    Constants.ANONYMOUS_USER_ID,
                    Constants.ANONYMOUS_USER_NAME,
                    Constants.DEFAULT_USER_COLOR
                });

            await Task.WhenAll(postsHavingWrongForumIdTask, forumsHavingWrongLastPostTask, topicsHavingWrongLastOrFirstPostTask);
        }

        private async Task CleanOperationLogs(IConfiguration config, ISqlExecuter dbConnection, ILogger logger, CancellationToken stoppingToken)
        {
            var retention = config.GetObject<TimeSpan?>("OperationLogsRetentionTime") ?? TimeSpan.FromDays(365);

            if (retention == TimeSpan.Zero)
            {
                logger.Information("Was instructed to keep operation logs indefinitely, will not delete.");
                return;
            }

            if (retention < TimeSpan.FromDays(1))
            {
                throw new ArgumentOutOfRangeException("OperationLogsRetentionTime", "Invalid app setting value.");
            }

            var toDelete = await dbConnection.QueryAsync<PhpbbLog>(
                "SELECT * FROM phpbb_log WHERE @now - log_time > @retention",
                new
                {
                    now = DateTime.UtcNow.ToUnixTimestamp(),
                    retention = retention.TotalSeconds
                });
               
            if (!toDelete.Any())
            {
                logger.Information("Nothing to delete from the operation logs.");
                return;
            }
            else
            {
                logger.Information("Deleting {count} items older than {retention} from the operation logs...", toDelete.Count(), retention);
            }

            stoppingToken.ThrowIfCancellationRequested();

            await dbConnection.ExecuteAsync(
                "DELETE FROM phpbb_log WHERE log_id IN @ids",
                new
                {
                    ids = toDelete.Select(l => l.LogId).DefaultIfEmpty()
                });
        }
    
        private TimeSpan GetTimeToWaitUntilRunIsAllowed(ITimeService timeService, IFileInfoService fileInfoService, CleanupServiceOptions options)
        {
            var now = timeService.DateTimeOffsetNow();
            if (options.MinimumAllowedRunTime.Date != now.Date || options.MaximumAllowedRunTime.Date != now.Date)
            {
                throw new ArgumentOutOfRangeException(
                    paramName: nameof(options),
                    message: $"The {nameof(options.MinimumAllowedRunTime)} and {nameof(options.MaximumAllowedRunTime)} properties of {nameof(CleanupServiceOptions)} should not have a date component.");
            }
            if (options.MinimumAllowedRunTime >= options.MaximumAllowedRunTime)
            {
                throw new ArgumentOutOfRangeException(
                    paramName: nameof(options),
                    message: $"The {nameof(options.MinimumAllowedRunTime)} and {nameof(options.MaximumAllowedRunTime)} properties of {nameof(CleanupServiceOptions)} should be correctly ordered.");
            }

            var timeUntilAllowedTimeFrame = GetTimeUntilAllowedRunTimeFrame();
            var timeSinceLastRun = GetElapsedTimeSinceLastRunIfAny();
            if (!timeSinceLastRun.HasValue || (timeSinceLastRun.Value + timeUntilAllowedTimeFrame >= options.Interval))
            {
                return timeUntilAllowedTimeFrame;
            }
            else
            {
                var toReturn = timeUntilAllowedTimeFrame;
                while (timeSinceLastRun.Value + toReturn < options.Interval)
                {
                    toReturn += TimeSpan.FromDays(1);
                }
                return toReturn;
            }

            TimeSpan? GetElapsedTimeSinceLastRunIfAny()
            {
               var lastRun = fileInfoService.GetLastWriteTime(OK_FILE_NAME);
               return lastRun.HasValue ? now.DateTime.ToUniversalTime() - lastRun.Value : null;
            }

            TimeSpan GetTimeUntilAllowedRunTimeFrame()
            {
                if (now < options.MinimumAllowedRunTime)
                {
                    return options.MinimumAllowedRunTime - now;
                }
                else if (now > options.MaximumAllowedRunTime)
                {
                    return options.MinimumAllowedRunTime + TimeSpan.FromDays(1) - now;
                }
                return TimeSpan.Zero;
            }
        }
    }
}
