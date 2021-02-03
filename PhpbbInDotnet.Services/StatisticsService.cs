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
                    var time = await _dbContext.PhpbbPosts.AsNoTracking().MinAsync(p => p.PostTime);
                    return new Statistics
                    {
                        FirstMessageDate = time == 0 ? null as DateTime? : time.ToUtcTime(),
                        UserCount = await _dbContext.PhpbbUsers.AsNoTracking().CountAsync(),
                        MessageCount = await _dbContext.PhpbbPosts.AsNoTracking().CountAsync(),
                        TopicCount = await _dbContext.PhpbbTopics.AsNoTracking().CountAsync(),
                        ForumCount = await _dbContext.PhpbbForums.AsNoTracking().CountAsync()
                    };
                }, 
                DateTimeOffset.UtcNow.AddMinutes(RefreshIntervalMinutes)
            );
    }
}
