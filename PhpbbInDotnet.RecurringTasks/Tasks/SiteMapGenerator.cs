using JW;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using PhpbbInDotnet.Database;
using PhpbbInDotnet.Domain;
using PhpbbInDotnet.Domain.Extensions;
using PhpbbInDotnet.Objects;
using PhpbbInDotnet.Services;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using XSitemaps;

namespace PhpbbInDotnet.RecurringTasks.Tasks
{
    class SiteMapGenerator : IRecurringTask
    {
        readonly IConfiguration _config;
        readonly ITimeService _timeService;
        readonly ISqlExecuter _sqlExecuter;
        readonly IStorageService _storageService;
        readonly IUserService _userService;
        readonly IForumTreeService _forumTreeService;
        readonly IWebHostEnvironment _webHostEnvironment;
        readonly ILogger _logger;

        public SiteMapGenerator(IConfiguration config, ITimeService timeService, ISqlExecuter sqlExecuter, IStorageService storageService, 
            IUserService userService, IForumTreeService forumTreeService, IWebHostEnvironment webHostEnvironment, ILogger logger)
        {
            _config = config;
            _timeService = timeService;
            _sqlExecuter = sqlExecuter;
            _storageService = storageService;
            _userService = userService;
            _forumTreeService = forumTreeService;
            _webHostEnvironment = webHostEnvironment;
            _logger = logger;
        }

        public async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.Information("Generating a new sitemap...");

            var anonymous = await _userService.GetAnonymousForumUserExpandedAsync();
            var allowedForums = await _forumTreeService.GetUnrestrictedForums(anonymous);
            var allSitemapUrls = GetForums(anonymous, allowedForums).Union(GetTopics(allowedForums));
            var urls = new ReadOnlyMemory<SitemapUrl>(await allSitemapUrls.ToArrayAsync(cancellationToken: stoppingToken));
            var siteMaps = Sitemap.Create(urls);

            var sitemapInfos = new List<SitemapInfo>();
            var serializeOptions = new SerializeOptions
            {
                EnableIndent = true,
                EnableGzipCompression = false,
            };

            foreach (var (sitemap, index) in siteMaps.Indexed())
            {
                stoppingToken.ThrowIfCancellationRequested();

                var sitemapName = $"sitemap_{index}.xml";
                var sitemapPath = Path.Combine(_webHostEnvironment.WebRootPath, sitemapName);
                using var sitemapStream = new FileStream(sitemapPath, FileMode.Create);
                sitemap.Serialize(sitemapStream, serializeOptions);
                sitemapInfos.Add(new SitemapInfo(
                    location: new Uri(new Uri(_config.GetValue<string>("BaseUrl")), sitemapName).ToString(),
                    modifiedAt: DateTimeOffset.UtcNow));
            }

            var sitemapIndex = new SitemapIndex(sitemapInfos);
            var sitemapIndexPath = Path.Combine(_webHostEnvironment.WebRootPath, $"sitemap.xml");
            using var sitemapIndexStream = new FileStream(sitemapIndexPath, FileMode.Create);
            sitemapIndex.Serialize(sitemapIndexStream, serializeOptions);

            _logger.Information("Sitemap generated successfully!");
        }

        async IAsyncEnumerable<SitemapUrl> GetForums(ForumUserExpanded anonymous, IEnumerable<int>? allowedForums)
        {
            var tree = await _forumTreeService.GetForumTree(anonymous, forceRefresh: false, fetchUnreadData: false);
            var maxTime = DateTime.MinValue;
            foreach (var forumId in allowedForums.EmptyIfNull())
            {
                var item = _forumTreeService.GetTreeNode(tree, forumId);

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
                    location: _forumTreeService.GetAbsoluteUrlToForum(item.ForumId),
                    modifiedAt: curTime,
                    frequency: GetChangeFrequency(curTime),
                    priority: GetForumPriority(item.Level));
            }

            yield return new SitemapUrl(
                location: _config.GetValue<string>("BaseUrl"),
                modifiedAt: maxTime,
                frequency: GetChangeFrequency(maxTime),
                priority: GetForumPriority(0));
        }

        async IAsyncEnumerable<SitemapUrl> GetTopics(IEnumerable<int>? allowedForums)
        {
            var topics = await _sqlExecuter.QueryAsync(
                @"WITH counts AS (
	                SELECT count(1) as post_count, 
		                    topic_id
	                    FROM phpbb_posts
                        WHERE forum_id IN @allowedForums
                        GROUP BY topic_id
                )
                SELECT t.topic_id,
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
                    var time = await _sqlExecuter.ExecuteScalarAsync<long>(
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
                        location: _forumTreeService.GetAbsoluteUrlToTopic((int)topic.topic_id, currentPage),
                        modifiedAt: lastChange,
                        frequency: freq,
                        priority: GetTopicPriority(freq));
                }
            }
        }

        static ChangeFrequency GetChangeFrequency(DateTime? lastChange)
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

        static double GetForumPriority(int forumLevel)
        {
            var values = new double[] { 1.0, 0.9, 0.8 };
            return values[Math.Min(forumLevel, 2)];
        }

        static double GetTopicPriority(ChangeFrequency changeFrequency)
        {
            var values = new double[] { 0.7, 0.6, 0.5, 0.4, 0.3 };
            return values[Math.Min((int)changeFrequency - 1, 4)];
        }
    }
}
