using Dapper;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PhpbbInDotnet.Database;
using PhpbbInDotnet.Database.Entities;
using PhpbbInDotnet.Objects;
using PhpbbInDotnet.Utilities;
using Serilog;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PhpbbInDotnet.Services
{
    public class DailyCleanupService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;

        public DailyCleanupService(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            //hacky hack https://github.com/dotnet/runtime/issues/36063
            await Task.Yield();

            using var scope = _serviceProvider.CreateScope();
            var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
            var dbContext = scope.ServiceProvider.GetRequiredService<ForumDbContext>();
            var utils = scope.ServiceProvider.GetRequiredService<CommonUtils>();
            var storageService = scope.ServiceProvider.GetRequiredService<StorageService>();
            var logger = scope.ServiceProvider.GetService<ILogger>();
            var writingToolsService = scope.ServiceProvider.GetService<WritingToolsService>();

            try
            {
                await Task.WhenAll(
                    CleanRecycleBin(config, dbContext, utils, storageService, logger, stoppingToken),
                    ResyncOrphanFiles(config, dbContext, stoppingToken),
                    ResyncForumsAndTopics(dbContext, stoppingToken)
                );

                stoppingToken.ThrowIfCancellationRequested();

                var (Message, IsSuccess) = await writingToolsService.DeleteOrphanedFiles();
                if (!(IsSuccess ?? false) && !string.IsNullOrWhiteSpace(Message))
                {
                    logger.Warning(Message);
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Failed at least one daily cleanup task. Application will continue.");
            }
        }

        private async Task CleanRecycleBin(IConfiguration config, ForumDbContext dbContext, CommonUtils utils, StorageService storageService, ILogger logger, CancellationToken stoppingToken)
        {
            var retention = config.GetObject<TimeSpan?>("RecycleBinRetentionTime") ?? TimeSpan.FromDays(7);
            var now = DateTime.UtcNow.ToUnixTimestamp();
            var toDelete = await (
                from rb in dbContext.PhpbbRecycleBin
                where now - rb.DeleteTime > retention.TotalSeconds
                select rb
            ).ToListAsync();

            if (!toDelete.Any())
            {
                logger.Information("Recycle bin is empty.");
                return;
            }

            dbContext.PhpbbRecycleBin.RemoveRange(toDelete);

            var posts = await Task.WhenAll(
                from i in toDelete
                where i.Type == RecycleBinItemType.Post
                select utils.DecompressObject<PostDto>(i.Content)
            );

            stoppingToken.ThrowIfCancellationRequested();

            var deleteResults = from p in posts
                                where p?.Attachments?.Any() ?? false

                                from a in p.Attachments
                                where !string.IsNullOrWhiteSpace(a?.PhysicalFileName)

                                select storageService.DeleteFile(a.PhysicalFileName, false);

            if (deleteResults.Any(r => !r))
            {
                logger.Warning("Not all attachments have been permanently deleted successfully. This could have happened due to them being already deleted.");
            }

            await dbContext.SaveChangesAsync(CancellationToken.None);
        }
    
        private async Task ResyncOrphanFiles(IConfiguration config, ForumDbContext dbContext, CancellationToken stoppingToken)
        {
            var retention = config.GetObject<TimeSpan?>("RecycleBinRetentionTime") ?? TimeSpan.FromDays(7);
            var now = DateTime.UtcNow.ToUnixTimestamp();
            var mismatchedOrphans = await (
                from a in dbContext.PhpbbAttachments

                join p in dbContext.PhpbbPosts
                on a.PostMsgId equals p.PostId
                into joinedPosts

                from jp in joinedPosts.DefaultIfEmpty()
                where jp == null && a.IsOrphan == 0 && now - a.Filetime > retention.TotalSeconds
                select a).ToListAsync();

            mismatchedOrphans.ForEach(a => a.IsOrphan = 1);

            stoppingToken.ThrowIfCancellationRequested();

            await dbContext.SaveChangesAsync(CancellationToken.None);
        }

        private async Task ResyncForumsAndTopics(ForumDbContext dbContext, CancellationToken stoppingToken)
        {
            var conn = await dbContext.GetDbConnectionAsync();

            var postsWithWrongForumId = await (
                from p in dbContext.PhpbbPosts.AsNoTracking()

                join t in dbContext.PhpbbTopics.AsNoTracking()
                on p.TopicId equals t.TopicId
                into joinedTopics

                from jt in joinedTopics
                where p.ForumId != jt.ForumId
                select new
                {
                    Post = p,
                    jt.ForumId
                }
            ).ToListAsync();
            var updatedPosts = postsWithWrongForumId.Select(x =>
            {
                x.Post.ForumId = x.ForumId;
                return x.Post;
            });

            dbContext.PhpbbPosts.UpdateRange(updatedPosts);

            stoppingToken.ThrowIfCancellationRequested();

            await dbContext.SaveChangesAsync(CancellationToken.None);

            var forumsHavingWrongLastPostTask = conn.QueryAsync<PhpbbForums, PhpbbPosts, (PhpbbForums Forum, PhpbbPosts Post)>(
                sql: @"WITH maxes AS (
	                    SELECT forum_id, MAX(post_time) AS post_time
                            FROM phpbb_posts
                            GROUP BY forum_id
                        ), last_posts AS (
                        SELECT DISTINCT p.*
                        FROM phpbb_posts p
                        JOIN maxes m ON p.forum_id = m.forum_id AND p.post_time = m.post_time
                    )
                    SELECT f.*, lp.*
                        FROM phpbb_forums f
                        JOIN last_posts lp ON f.forum_id = lp.forum_id
                        WHERE lp.post_id <> f.forum_last_post_id",
                map: (forum, post) => (forum, post),
                splitOn: "post_id"
            );
            var topicsHavingWrongLastPostTask = conn.QueryAsync<PhpbbTopics, PhpbbPosts, (PhpbbTopics Topic, PhpbbPosts Post)>(
                sql: @"WITH maxes AS (
	                    SELECT topic_id, MAX(post_time) AS post_time
                            FROM phpbb_posts
                            GROUP BY topic_id
                        ), last_posts AS (
                        SELECT DISTINCT p.*
                        FROM phpbb_posts p
                        JOIN maxes m ON p.topic_id = m.topic_id AND p.post_time = m.post_time
                    )
                    SELECT t.*, lp.*
                        FROM phpbb_topics t
                        JOIN last_posts lp ON t.topic_id = lp.topic_id
                        WHERE lp.post_id <> t.topic_last_post_id",
                map: (topic, post) => (topic, post),
                splitOn: "post_id"
            );
            var topicsHavingWrongFirstPostTask = conn.QueryAsync<PhpbbTopics, PhpbbPosts, (PhpbbTopics Topic, PhpbbPosts Post)>(
                sql: @"WITH mins AS (
	                    SELECT topic_id, MIN(post_time) AS post_time
                            FROM phpbb_posts
                            GROUP BY topic_id
                        ), first_posts AS (
                        SELECT DISTINCT p.*
                        FROM phpbb_posts p
                        JOIN mins m ON p.topic_id = m.topic_id AND p.post_time = m.post_time
                    )
                    SELECT t.*, fp.*
                        FROM phpbb_topics t
                        JOIN first_posts fp ON t.topic_id = fp.topic_id
                        WHERE fp.post_id <> t.topic_first_post_id",
                map: (topic, post) => (topic, post),
                splitOn: "post_id"
            );
            await Task.WhenAll(forumsHavingWrongLastPostTask, topicsHavingWrongLastPostTask, topicsHavingWrongFirstPostTask);

            var fixedForumsTask = Task.WhenAll((await forumsHavingWrongLastPostTask).Select(async item =>
            {
                var user = await dbContext.PhpbbUsers.AsNoTracking().FirstOrDefaultAsync(u => u.UserId == item.Post.PosterId);
                item.Forum.ForumLastPostId = item.Post.PostId;
                item.Forum.ForumLastPosterId = user?.UserId ?? Constants.ANONYMOUS_USER_ID;
                item.Forum.ForumLastPostSubject = item.Post.PostSubject;
                item.Forum.ForumLastPostTime = item.Post.PostTime;
                item.Forum.ForumLastPosterName = user?.Username ?? Constants.ANONYMOUS_USER_NAME;
                item.Forum.ForumLastPosterColour = user?.UserColour ?? Constants.DEFAULT_USER_COLOR;
                return item.Forum;
            }));
            var fixedLastPostTopicsTask = Task.WhenAll((await topicsHavingWrongLastPostTask).Select(async item =>
            {
                var user = await dbContext.PhpbbUsers.AsNoTracking().FirstOrDefaultAsync(u => u.UserId == item.Post.PosterId);
                item.Topic.TopicLastPostId = item.Post.PostId;
                item.Topic.TopicLastPosterId = user?.UserId ?? Constants.ANONYMOUS_USER_ID;
                item.Topic.TopicLastPostSubject = item.Post.PostSubject;
                item.Topic.TopicLastPostTime = item.Post.PostTime;
                item.Topic.TopicLastPosterName = user?.Username ?? Constants.ANONYMOUS_USER_NAME;
                item.Topic.TopicLastPosterColour = user?.UserColour ?? Constants.DEFAULT_USER_COLOR;
                return item.Topic;
            }));
            var fixedFirstPostTopicsTask = Task.WhenAll((await topicsHavingWrongFirstPostTask).Select(async item =>
            {
                var user = await dbContext.PhpbbUsers.AsNoTracking().FirstOrDefaultAsync(u => u.UserId == item.Post.PosterId);
                item.Topic.TopicFirstPostId = item.Post.PostId;
                item.Topic.TopicFirstPosterName = user?.Username ?? Constants.ANONYMOUS_USER_NAME;
                item.Topic.TopicFirstPosterColour = user?.UserColour ?? Constants.DEFAULT_USER_COLOR;
                return item.Topic;
            }));
            await Task.WhenAll(fixedForumsTask, fixedLastPostTopicsTask, fixedFirstPostTopicsTask);

            var fixedForums = await fixedForumsTask;
            var fixedLastPostTopics = await fixedLastPostTopicsTask;
            var fixedFirstPostTopics = await fixedFirstPostTopicsTask;

            dbContext.PhpbbForums.UpdateRange(fixedForums);
            dbContext.PhpbbTopics.UpdateRange(fixedLastPostTopics.Union(fixedFirstPostTopics, new TopicEqualityComparer()));

            stoppingToken.ThrowIfCancellationRequested();

            await dbContext.SaveChangesAsync(CancellationToken.None);
        }

        class TopicEqualityComparer : IEqualityComparer<PhpbbTopics>
        {
            public bool Equals(PhpbbTopics x, PhpbbTopics y)
                => x != null && y != null && x.TopicId == y.TopicId;

            public int GetHashCode([DisallowNull] PhpbbTopics obj)
                => obj.TopicId.GetHashCode();
        }
    }
}
