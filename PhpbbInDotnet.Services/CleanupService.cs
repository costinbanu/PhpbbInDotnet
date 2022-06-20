using JW;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PhpbbInDotnet.Database;
using PhpbbInDotnet.Database.Entities;
using PhpbbInDotnet.Objects;
using PhpbbInDotnet.Objects.Configuration;
using PhpbbInDotnet.Utilities;
using PhpbbInDotnet.Utilities.Core;
using PhpbbInDotnet.Utilities.Extensions;
using Serilog;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using XSitemaps;

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
            var storageService = scope.ServiceProvider.GetRequiredService<IStorageService>();
            var logger = scope.ServiceProvider.GetRequiredService<ILogger>();
            var writingToolsService = scope.ServiceProvider.GetRequiredService<IWritingToolsService>();
            var timeService = scope.ServiceProvider.GetRequiredService<ITimeService>();
            var fileInfoService = scope.ServiceProvider.GetRequiredService<IFileInfoService>();
            var userService = scope.ServiceProvider.GetRequiredService<IUserService>();
            var forumService = scope.ServiceProvider.GetRequiredService<IForumTreeService>();
            var environment = scope.ServiceProvider.GetRequiredService<IWebHostEnvironment>();

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
                    CleanRecycleBin(config, timeService, sqlExecuter, storageService, logger, stoppingToken),
                    ResyncOrphanFiles(config, timeService, sqlExecuter, stoppingToken),
                    ResyncForumsAndTopics(sqlExecuter, stoppingToken),
                    CleanOperationLogs(config, timeService, sqlExecuter, logger, stoppingToken),
                    GenerateSiteMap(config, sqlExecuter, userService, forumService, environment, stoppingToken)
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

        private async Task CleanRecycleBin(IConfiguration config, ITimeService timeService, ISqlExecuter sqlExecuter, IStorageService storageService, ILogger logger, CancellationToken stoppingToken)
        {
            var retention = config.GetObject<TimeSpan?>("RecycleBinRetentionTime") ?? TimeSpan.FromDays(7);

            if (retention < TimeSpan.FromDays(1))
            {
                throw new ArgumentOutOfRangeException("RecycleBinRetentionTime", "Invalid app setting value.");
            }

            var toDelete = await sqlExecuter.QueryAsync<PhpbbRecycleBin>(
                "SELECT * FROM phpbb_recycle_bin WHERE @now - delete_time > @retention",
                new
                {
                    now = timeService.DateTimeUtcNow().ToUnixTimestamp(),
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

            await sqlExecuter.ExecuteAsync(
                "DELETE FROM phpbb_recycle_bin WHERE type = @type AND id = @id",
                toDelete);

            var posts = await Task.WhenAll(
                from i in toDelete
                where i.Type == RecycleBinItemType.Post
                select CompressionUtils.DecompressObject<PostDto>(i.Content)
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

        private async Task ResyncOrphanFiles(IConfiguration config, ITimeService timeService, ISqlExecuter sqlExecuter, CancellationToken stoppingToken)
        {
            var retention = config.GetObject<TimeSpan?>("RecycleBinRetentionTime") ?? TimeSpan.FromDays(7);

            stoppingToken.ThrowIfCancellationRequested();

            await sqlExecuter.ExecuteAsync(
                @"UPDATE phpbb_attachments a
                    LEFT JOIN phpbb_posts p ON a.post_msg_id = p.post_id
                     SET a.is_orphan = 1
                   WHERE p.post_id IS NULL AND @now - a.filetime > @retention AND a.is_orphan = 0",
                new
                {
                    now = timeService.DateTimeUtcNow().ToUnixTimestamp(),
                    retention = retention.TotalSeconds
                });
        }

        private async Task ResyncForumsAndTopics(ISqlExecuter sqlExecuter, CancellationToken stoppingToken)
        {
            stoppingToken.ThrowIfCancellationRequested();

            var postsHavingWrongForumIdTask = sqlExecuter.ExecuteAsync(
                @"UPDATE phpbb_posts p
                    JOIN phpbb_topics t ON p.topic_id = t.topic_id
                     SET p.forum_id = t.forum_id
                   WHERE p.forum_id <> t.forum_id");

            var forumsHavingWrongLastPostTask = sqlExecuter.ExecuteAsync(
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

            var topicsHavingWrongLastOrFirstPostTask = sqlExecuter.ExecuteAsync(
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

        private async Task CleanOperationLogs(IConfiguration config, ITimeService timeService, ISqlExecuter sqlExecuter, ILogger logger, CancellationToken stoppingToken)
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

            var toDelete = await sqlExecuter.QueryAsync<PhpbbLog>(
                "SELECT * FROM phpbb_log WHERE @now - log_time > @retention",
                new
                {
                    now = timeService.DateTimeUtcNow().ToUnixTimestamp(),
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

            await sqlExecuter.ExecuteAsync(
                "DELETE FROM phpbb_log WHERE log_id IN @ids",
                new
                {
                    ids = toDelete.Select(l => l.LogId).DefaultIfEmpty()
                });
        }

        private async Task GenerateSiteMap(IConfiguration config, ISqlExecuter sqlExecuter, IUserService userService, IForumTreeService forumTreeService, IWebHostEnvironment env, CancellationToken stoppingToken)
        {
            var claimsPrincipalTask = userService.GetAnonymousClaimsPrincipal();
            var permissionsTask = userService.GetPermissions(Constants.ANONYMOUS_USER_ID);
            await Task.WhenAll(claimsPrincipalTask, permissionsTask);

            var anonymous = userService.ClaimsPrincipalToAuthenticatedUser(await claimsPrincipalTask)!;
            anonymous.AllPermissions = await permissionsTask;

            var allowedForums = await forumTreeService.GetUnrestrictedForums(anonymous);
            var allSitemapUrls = GetForums().Union(GetTopics());
            var urls = new ReadOnlyMemory<SitemapUrl>(await allSitemapUrls.ToArrayAsync(cancellationToken: stoppingToken));
            var siteMaps = Sitemap.Create(urls);

            var infos = new List<SitemapInfo>();
            var options = new SerializeOptions
            {
                EnableIndent = true,
                EnableGzipCompression = false,
            };
            foreach (var (item, index) in siteMaps.Indexed())
            {
                stoppingToken.ThrowIfCancellationRequested();

                var name = $"sitemap_{index}.xml";
                var path = Path.Combine(env.WebRootPath, name);
                using var stream = new FileStream(path, FileMode.Create);
                item.Serialize(stream, options);
                infos.Add(new SitemapInfo(new Uri(new Uri(config.GetValue<string>("BaseUrl")), name).ToString(), DateTimeOffset.UtcNow));
            }

            var siteMapIndex = new SitemapIndex(infos);
            var indexPath = Path.Combine(env.WebRootPath, $"sitemap.xml");
            using var indexStream = new FileStream(indexPath, FileMode.Create);
            siteMapIndex.Serialize(indexStream, options);

            async IAsyncEnumerable<SitemapUrl> GetForums()
            {
                var tree = await forumTreeService.GetForumTree(anonymous, forceRefresh: false, fetchUnreadData: false);
                var maxTime = DateTime.MinValue;
                foreach (var forumId in allowedForums.EmptyIfNull())
                {
                    var item = forumTreeService.GetTreeNode(tree, forumId);

                    if (item is null || item.ForumId < 1)
                    {
                        continue;
                    }

                    var curTime = item.ForumLastPostTime?.ToUtcTime();
                    if (curTime > maxTime)
                    {
                        maxTime = curTime.Value;
                    }

                    yield return new SitemapUrl(
                        location: forumTreeService.GetAbsoluteUrlToForum(item.ForumId),
                        modifiedAt: curTime,
                        frequency: GetChangeFrequency(curTime),
                        priority: GetForumPriority(item.Level));
                }

                yield return new SitemapUrl(
                    location: config.GetValue<string>("BaseUrl"),
                    modifiedAt: maxTime,
                    frequency: GetChangeFrequency(maxTime),
                    priority: GetForumPriority(0));
            }

            async IAsyncEnumerable<SitemapUrl> GetTopics()
            {
                var topics = await sqlExecuter.QueryAsync(
                    @"WITH counts AS (
	                SELECT count(1) as post_count, 
		                   topic_id
	                  FROM phpbb_posts
                     WHERE forum_id IN @allowedForums
                     GROUP BY topic_id
                )
                SELECT t.topic_id,
	                   t.topic_last_post_time,
                       c.post_count
                  FROM phpbb_topics t
                  JOIN counts c
                    ON c.topic_id = t.topic_id 
                 WHERE t.forum_id IN @allowedForums",
                    new
                    {
                        allowedForums
                    });

                foreach (var topic in topics)
                {
                    var pager = new Pager(totalItems: (int)topic.post_count, pageSize: Constants.DEFAULT_PAGE_SIZE);
                    for (var currentPage = 1; currentPage <= pager.TotalPages; currentPage++)
                    {
                        var time = await sqlExecuter.ExecuteScalarAsync<long>(
                            @"WITH times AS (
	                        SELECT post_time, post_edit_time
	                        FROM phpbb_posts
	                        WHERE topic_id = @topicId
	                        ORDER BY post_time
	                        LIMIT @skip, @take
                        )
                        SELECT greatest(max(post_time), max(post_edit_time)) AS max_time
                        FROM times",
                            new
                            {
                                topicId = topic.topic_id,
                                skip = (currentPage - 1) * Constants.DEFAULT_PAGE_SIZE,
                                take = Constants.DEFAULT_PAGE_SIZE
                            });

                        var lastChange = time.ToUtcTime();
                        var freq = GetChangeFrequency(lastChange);
                        yield return new SitemapUrl(
                            location: forumTreeService.GetAbsoluteUrlToTopic((int)topic.topic_id, currentPage),
                            modifiedAt: lastChange,
                            frequency: freq,
                            priority: GetTopicPriority(freq));
                    }
                }
            }

            ChangeFrequency GetChangeFrequency(DateTime? lastChange)
            {
                if (lastChange is null)
                {
                    return ChangeFrequency.Never;
                }

                var diff = DateTime.UtcNow - lastChange.Value;
                if (diff.TotalDays < 1)
                {
                    return ChangeFrequency.Hourly;
                }
                else if (diff.TotalDays < 7)
                {
                    return ChangeFrequency.Daily;
                }
                else if (diff.TotalDays < 30)
                {
                    return ChangeFrequency.Weekly;
                }
                else if (diff.TotalDays < 365)
                {
                    return ChangeFrequency.Monthly;
                }
                else
                {
                    return ChangeFrequency.Yearly;
                }
            }

            double GetForumPriority(int forumLevel)
            {
                var values = new double[] { 1.0, 0.9, 0.8 };
                return values[Math.Min(forumLevel, 2)];
            }

            double GetTopicPriority(ChangeFrequency changeFrequency)
            {
                var values = new double[] { 0.7, 0.6, 0.5, 0.4, 0.3 };
                return values[Math.Min((int)changeFrequency - 1, 4)];
            }
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
