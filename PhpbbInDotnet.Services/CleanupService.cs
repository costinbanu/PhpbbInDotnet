using Dapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PhpbbInDotnet.Database;
using PhpbbInDotnet.Database.Entities;
using PhpbbInDotnet.Objects;
using PhpbbInDotnet.Utilities;
using Serilog;
using System;
using System.Data.Common;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PhpbbInDotnet.Services
{
    public class CleanupService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly string _fileName;

        public CleanupService(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
            _fileName = $"{nameof(CleanupService)}.ok";
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            //hacky hack https://github.com/dotnet/runtime/issues/36063
            await Task.Yield();

            using var scope = _serviceProvider.CreateScope();
            var config = scope.ServiceProvider.GetRequiredService<IConfiguration>()!;
            var dbContext = scope.ServiceProvider.GetRequiredService<ForumDbContext>()!;
            var utils = scope.ServiceProvider.GetRequiredService<CommonUtils>()!;
            var storageService = scope.ServiceProvider.GetRequiredService<StorageService>()!;
            var logger = scope.ServiceProvider.GetService<ILogger>()!;
            var writingToolsService = scope.ServiceProvider.GetService<WritingToolsService>()!;

            logger.Information("Launching a new {name} instance...", nameof(CleanupService));

            var interval = config.GetObject<TimeSpan?>("CleanupServiceInterval") ?? TimeSpan.FromDays(1);
            DateTime? lastRun = null;
            try
            {
                lastRun = new FileInfo(_fileName).LastWriteTimeUtc;
            }
            catch { }

            TimeSpan? elapsed = lastRun.HasValue ? DateTime.UtcNow - lastRun.Value : null;
            if (elapsed.HasValue && elapsed < interval)
            {
                Log.Information("Waiting {time} before executing cleanup tasks...", interval - elapsed.Value);
                stoppingToken.WaitHandle.WaitOne(interval - elapsed.Value);
            }

            try
            {
                stoppingToken.ThrowIfCancellationRequested();

                logger.Information("Executing cleanup tasks NOW...");

                var connection = dbContext.GetDbConnection();
                await Task.WhenAll(
                    CleanRecycleBin(config, connection, utils, storageService, logger, stoppingToken),
                    ResyncOrphanFiles(config, connection, stoppingToken),
                    ResyncForumsAndTopics(connection, stoppingToken),
                    CleanOperationLogs(config, connection, logger, stoppingToken)
                );

                stoppingToken.ThrowIfCancellationRequested();

                var (Message, IsSuccess) = await writingToolsService.DeleteOrphanedFiles();
                if (!(IsSuccess ?? false) && !string.IsNullOrWhiteSpace(Message))
                {
                    logger.Warning(Message);
                }

                File.WriteAllText(_fileName, string.Empty);
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Failed at least one cleanup task. Application will continue.");
            }
        }

        private async Task CleanRecycleBin(IConfiguration config, DbConnection dbConnection, CommonUtils utils, StorageService storageService, ILogger logger, CancellationToken stoppingToken)
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

        private async Task ResyncOrphanFiles(IConfiguration config, DbConnection dbConnection, CancellationToken stoppingToken)
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

        private async Task ResyncForumsAndTopics(DbConnection dbConnection, CancellationToken stoppingToken)
        {
            stoppingToken.ThrowIfCancellationRequested();

            var postsHavingWrongForumIdTask = dbConnection.ExecuteAsync(
                @"UPDATE phpbb_posts p
                    JOIN phpbb_topics t ON p.topic_id = t.topic_id
                     SET p.forum_id = t.forum_id
                   WHERE p.forum_id <> t.forum_id");

            var forumsHavingWrongLastPostTask = dbConnection.ExecuteAsync(
                @"WITH maxes AS (
	                SELECT forum_id, MAX(post_time) AS post_time
	                 FROM phpbb_posts
	                GROUP BY forum_id
                ), last_posts AS (
	                SELECT DISTINCT p.*
	                  FROM phpbb_posts p
	                  JOIN maxes m ON p.forum_id = m.forum_id AND p.post_time = m.post_time
                )
                UPDATE phpbb_forums f
                  JOIN last_posts lp ON f.forum_id = lp.forum_id
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
                @"WITH maxes AS (
	                  SELECT topic_id, MAX(post_time) AS post_time
		                FROM phpbb_posts
		                GROUP BY topic_id
                ), last_posts AS (
	                SELECT DISTINCT p.*
	                  FROM phpbb_posts p
	                  JOIN maxes m ON p.topic_id = m.topic_id AND p.post_time = m.post_time
                ), mins AS (
	                SELECT topic_id, MIN(post_time) AS post_time
	                  FROM phpbb_posts
	                 GROUP BY topic_id
                ), first_posts AS (
	                SELECT DISTINCT p.*
	                  FROM phpbb_posts p
	                  JOIN mins m ON p.topic_id = m.topic_id AND p.post_time = m.post_time
                )
                UPDATE phpbb_topics t
                  JOIN last_posts lp ON t.topic_id = lp.topic_id
                  JOIN first_posts fp ON t.topic_id = fp.topic_id
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

        private async Task CleanOperationLogs(IConfiguration config, DbConnection dbConnection, ILogger logger, CancellationToken stoppingToken)
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
    }
}
