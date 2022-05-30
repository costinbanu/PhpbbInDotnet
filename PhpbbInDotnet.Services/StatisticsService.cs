using Dapper;
using LazyCache;
using PhpbbInDotnet.Database;
using PhpbbInDotnet.Objects;
using PhpbbInDotnet.Utilities;
using System;
using System.Threading.Tasks;

namespace PhpbbInDotnet.Services
{
    class StatisticsService : IStatisticsService
    {
        const string CACHE_KEY = "ForumStatistics";

        private readonly IAppCache _cache;
        private readonly IForumDbContext _dbContext;

        public int RefreshIntervalMinutes => 30;

        public StatisticsService(IAppCache cache, IForumDbContext dbContext)
        {
            _cache = cache;
            _dbContext = dbContext;
        }

        public async Task<Statistics> GetStatistics()
            => await _cache.GetOrAddAsync(
                CACHE_KEY,
                async () =>
                {
                    var sqlExecuter = _dbContext.GetSqlExecuter();
                    var timeTask = sqlExecuter.ExecuteScalarAsync<long>("SELECT min(post_time) FROM phpbb_posts");
                    var userTask = sqlExecuter.ExecuteScalarAsync<int>("SELECT count(1) FROM phpbb_users");
                    var postTask = sqlExecuter.ExecuteScalarAsync<int>("SELECT count(1) FROM phpbb_posts");
                    var topicTask = sqlExecuter.ExecuteScalarAsync<int>("SELECT count(1) FROM phpbb_topics");
                    var forumTask = sqlExecuter.ExecuteScalarAsync<int>("SELECT count(1) FROM phpbb_forums");
                    await Task.WhenAll(timeTask, userTask, postTask, topicTask, forumTask);

                    return new Statistics
                    {
                        FirstMessageDate = (await timeTask) == 0 ? null : (await timeTask).ToUtcTime(),
                        UserCount = await userTask,
                        PostCount = await postTask,
                        TopicCount = await topicTask,
                        ForumCount = await forumTask
                    };
                },
                DateTimeOffset.UtcNow.AddMinutes(RefreshIntervalMinutes)
            );
    }
}
