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

        private readonly CacheService _cacheService;
        private readonly ForumDbContext _dbContext;

        public int RefreshIntervalMinutes => 30;

        public StatisticsService(CacheService cacheService, ForumDbContext dbContext)
        {
            _cacheService = cacheService;
            _dbContext = dbContext;
        }

        public async Task<Statistics> GetStatistics()
        {
            var cached = await _cacheService.GetFromCache<Statistics>(CACHE_KEY);
            if (cached == null)
            {
                var time = await _dbContext.PhpbbPosts.AsNoTracking().MinAsync(p => p.PostTime);
                cached = new Statistics
                {
                    FirstMessageDate = time == 0 ? null as DateTime? : time.ToUtcTime(),
                    UserCount = await _dbContext.PhpbbUsers.AsNoTracking().CountAsync(),
                    MessageCount = await _dbContext.PhpbbPosts.AsNoTracking().CountAsync(),
                    TopicCount = await _dbContext.PhpbbTopics.AsNoTracking().CountAsync(),
                    ForumCount = await _dbContext.PhpbbForums.AsNoTracking().CountAsync()
                };
                await _cacheService.SetInCache(CACHE_KEY, cached, TimeSpan.FromMinutes(RefreshIntervalMinutes), true);
            }
            return cached;
        }
    }
}
