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

        public async Task<Statistics> GetStatistics()
            => await _cache.GetOrAddAsync("ForumStatistics", GetStatisticsImpl, DateTimeOffset.UtcNow.AddMinutes(30));

        async Task<Statistics> GetStatisticsImpl()
        {
            var sqlExecuter = _dbContext.GetSqlExecuter();
            var twentyFourHoursAgo = DateTime.UtcNow.AddHours(-24).ToUnixTimestamp();

            var timeTask = sqlExecuter.ExecuteScalarAsync<long>("SELECT min(post_time) FROM phpbb_posts");
            var userTask = sqlExecuter.ExecuteScalarAsync<int>("SELECT count(1) FROM phpbb_users");
            var postTask = sqlExecuter.ExecuteScalarAsync<int>("SELECT count(1) FROM phpbb_posts");
            var topicTask = sqlExecuter.ExecuteScalarAsync<int>("SELECT count(1) FROM phpbb_topics");
            var forumTask = sqlExecuter.ExecuteScalarAsync<int>("SELECT count(1) FROM phpbb_forums");
            var latestUsersTask = sqlExecuter.ExecuteScalarAsync<int>("SELECT count(1) FROM phpbb_users WHERE user_lastvisit >= @twentyFourHoursAgo", new { twentyFourHoursAgo });
            var latestPostsTask = sqlExecuter.ExecuteScalarAsync<int>("SELECT count(1) FROM phpbb_posts WHERE post_time >= @twentyFourHoursAgo", new { twentyFourHoursAgo });
            var latestFilesTask = sqlExecuter.ExecuteScalarAsync<long>("SELECT sum(filesize) FROM phpbb_attachments WHERE filetime >= @twentyFourHoursAgo", new { twentyFourHoursAgo });
            
            await Task.WhenAll(timeTask, userTask, postTask, topicTask, forumTask, latestUsersTask, latestPostsTask, latestFilesTask);

            var time = await timeTask;
            return new Statistics
            {
                FirstMessageDate = time == 0 ? null : time.ToUtcTime(),
                UserCount = await userTask,
                PostCount = await postTask,
                TopicCount = await topicTask,
                ForumCount = await forumTask,
                LatestPostsCount = await latestPostsTask,
                LatestUsersCount = await latestUsersTask,
                LatestFileSizeSum = await latestFilesTask
            };
        }
    }
}
