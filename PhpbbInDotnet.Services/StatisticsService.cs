using LazyCache;
using PhpbbInDotnet.Database.SqlExecuter;
using PhpbbInDotnet.Domain.Extensions;
using PhpbbInDotnet.Objects;
using System;
using System.Threading.Tasks;

namespace PhpbbInDotnet.Services
{
    class StatisticsService : IStatisticsService
    {
        private readonly IAppCache _cache;
        private readonly ISqlExecuter _sqlExecuter;

        public StatisticsService(IAppCache cache, ISqlExecuter sqlExecuter)
        {
            _cache = cache;
            _sqlExecuter = sqlExecuter;
        }

        public async Task<Statistics> GetStatisticsSummary()
            => await _cache.GetOrAddAsync("ForumStatistics", GetStatisticsImpl, DateTimeOffset.UtcNow.AddMinutes(30));

        async Task<Statistics> GetStatisticsImpl()
        {
            var time = await _sqlExecuter.ExecuteScalarAsync<long>("SELECT min(post_time) FROM phpbb_posts");
            return new Statistics
            {
                FirstMessageDate = time == 0 ? null : time.ToUtcTime(),
                UserCount = await _sqlExecuter.ExecuteScalarAsync<int>("SELECT count(1) FROM phpbb_users"),
                PostCount = await _sqlExecuter.ExecuteScalarAsync<int>("SELECT count(1) FROM phpbb_posts"),
                TopicCount = await _sqlExecuter.ExecuteScalarAsync<int>("SELECT count(1) FROM phpbb_topics"),
                ForumCount = await _sqlExecuter.ExecuteScalarAsync<int>("SELECT count(1) FROM phpbb_forums"),

            };
        }

        public async Task<TimedStatistics> GetTimedStatistics(DateTime? startTime)
        {
            var since = startTime?.ToUnixTimestamp();
            return new TimedStatistics
            {
                PostsCount = await _sqlExecuter.ExecuteScalarAsync<int>(
                    "SELECT count(1) FROM phpbb_posts WHERE @since IS NULL OR post_time >= @since",
                    new { since }),
                UsersCount = await _sqlExecuter.ExecuteScalarAsync<int>(
                    "SELECT count(1) FROM phpbb_users WHERE @since IS NULL OR user_lastvisit >= @since",
                    new { since }),
                FileSizeSum = await _sqlExecuter.ExecuteScalarAsync<long>(
                    "SELECT sum(filesize) FROM phpbb_attachments WHERE @since IS NULL OR filetime >= @since",
                    new { since }),
                FileCount = await _sqlExecuter.ExecuteScalarAsync<long>(
                    "SELECT count(1) FROM phpbb_attachments WHERE @since IS NULL OR filetime >= @since",
                    new { since })
            };
        }
    }
}
