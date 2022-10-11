using LazyCache;
using PhpbbInDotnet.Database;
using PhpbbInDotnet.Domain.Extensions;
using PhpbbInDotnet.Objects;
using System;
using System.Threading.Tasks;

namespace PhpbbInDotnet.Services
{
    class StatisticsService : IStatisticsService
    {
        private readonly IAppCache _cache;
        private readonly IForumDbContext _dbContext;

        public StatisticsService(IAppCache cache, IForumDbContext dbContext)
        {
            _cache = cache;
            _dbContext = dbContext;
        }

        public async Task<Statistics> GetStatisticsSummary()
            => await _cache.GetOrAddAsync("ForumStatistics", GetStatisticsImpl, DateTimeOffset.UtcNow.AddMinutes(30));

        async Task<Statistics> GetStatisticsImpl()
        {
            var sqlExecuter = _dbContext.GetSqlExecuter();
            var since = DateTime.UtcNow.AddHours(-24).ToUnixTimestamp();

            var timeTask = sqlExecuter.ExecuteScalarAsync<long>("SELECT min(post_time) FROM phpbb_posts");
            var userTask = sqlExecuter.ExecuteScalarAsync<int>("SELECT count(1) FROM phpbb_users");
            var postTask = sqlExecuter.ExecuteScalarAsync<int>("SELECT count(1) FROM phpbb_posts");
            var topicTask = sqlExecuter.ExecuteScalarAsync<int>("SELECT count(1) FROM phpbb_topics");
            var forumTask = sqlExecuter.ExecuteScalarAsync<int>("SELECT count(1) FROM phpbb_forums");
            
            await Task.WhenAll(timeTask, userTask, postTask, topicTask, forumTask);

            var time = await timeTask;
            return new Statistics
            {
                FirstMessageDate = time == 0 ? null : time.ToUtcTime(),
                UserCount = await userTask,
                PostCount = await postTask,
                TopicCount = await topicTask,
                ForumCount = await forumTask,

            };
        }

        public async Task<TimedStatistics> GetTimedStatistics(DateTime? startTime)
        {
            var sqlExecuter = _dbContext.GetSqlExecuter();
            var since = startTime?.ToUnixTimestamp();

            var usersTask = sqlExecuter.ExecuteScalarAsync<int>(
                "SELECT count(1) FROM phpbb_users WHERE @since IS NULL OR user_lastvisit >= @since", 
                new { since });
            var postsTask = sqlExecuter.ExecuteScalarAsync<int>(
                "SELECT count(1) FROM phpbb_posts WHERE @since IS NULL OR post_time >= @since", 
                new { since });
            var fileSizeTask = sqlExecuter.ExecuteScalarAsync<long>(
                "SELECT sum(filesize) FROM phpbb_attachments WHERE @since IS NULL OR filetime >= @since", 
                new { since });
            var fileCountTask = sqlExecuter.ExecuteScalarAsync<long>(
                "SELECT count(1) FROM phpbb_attachments WHERE @since IS NULL OR filetime >= @since",
                new { since });

            await Task.WhenAll(usersTask, postsTask, fileSizeTask, fileCountTask);

            return new TimedStatistics
            {
                PostsCount = await postsTask,
                UsersCount = await usersTask,
                FileSizeSum = await fileSizeTask,
                FileCount = await fileCountTask
            };
        }
    }
}
