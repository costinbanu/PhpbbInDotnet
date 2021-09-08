using LazyCache;
using Microsoft.EntityFrameworkCore;
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
                    var timeTask = _dbContext.PhpbbPosts.AsNoTracking().MinAsync(p => p.PostTime);
                    var userTask = _dbContext.PhpbbUsers.AsNoTracking().CountAsync();
                    var messageTask = _dbContext.PhpbbPosts.AsNoTracking().CountAsync();
                    var topicTask = _dbContext.PhpbbTopics.AsNoTracking().CountAsync();
                    var forumTask = _dbContext.PhpbbForums.AsNoTracking().CountAsync();
                    await Task.WhenAll(timeTask, userTask, messageTask, topicTask, forumTask);

                    return new Statistics
                    {
                        FirstMessageDate = (await timeTask) == 0 ? null : (await timeTask).ToUtcTime(),
                        UserCount = await userTask,
                        MessageCount = await messageTask,
                        TopicCount = await topicTask,
                        ForumCount = await forumTask
                    };
                },
                DateTimeOffset.UtcNow.AddMinutes(RefreshIntervalMinutes)
            );
    }
}
