using Dapper;
using LazyCache;
using PhpbbInDotnet.Database;
using PhpbbInDotnet.Objects;
using PhpbbInDotnet.Utilities;
using System;
using System.Threading.Tasks;

namespace PhpbbInDotnet.Services
{
    public class StatisticsService
    {
        const string CACHE_KEY = "ForumStatistics";

        private readonly IAppCache _cache;
        private readonly ForumDbContext _dbContext;

        public int RefreshIntervalMinutes => 30;

        public StatisticsService(IAppCache cache, ForumDbContext dbContext)
        {
            _cache = cache;
            _dbContext = dbContext;
        }

        public async Task<Statistics> GetStatistics()
            => await _cache.GetOrAddAsync(
                CACHE_KEY,
                async () =>
                {
                    var conn = _dbContext.GetDbConnection();
                    var timeTask = conn.ExecuteScalarAsync<long>("SELECT min(post_time) FROM phpbb_posts");
                    var userTask = conn.ExecuteScalarAsync<int>("SELECT count(1) FROM phpbb_users");
                    var postTask = conn.ExecuteScalarAsync<int>("SELECT count(1) FROM phpbb_posts");
                    var topicTask = conn.ExecuteScalarAsync<int>("SELECT count(1) FROM phpbb_topics");
                    var forumTask = conn.ExecuteScalarAsync<int>("SELECT count(1) FROM phpbb_forums");
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
