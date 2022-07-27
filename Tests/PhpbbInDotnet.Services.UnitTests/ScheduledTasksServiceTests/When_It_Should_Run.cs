using Microsoft.Extensions.DependencyInjection;
using Moq;
using PhpbbInDotnet.Database.Entities;
using PhpbbInDotnet.Objects;
using PhpbbInDotnet.Services.UnitTests.Utils;
using PhpbbInDotnet.Domain;
using PhpbbInDotnet.Domain.Utilities;
using PhpbbInDotnet.Domain.Extensions;
using RandomTestValues;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace PhpbbInDotnet.Services.UnitTests.ScheduledTasksServiceTests
{
    public class When_It_Should_Run : ScheduledTasksServiceTestsBase
    {
        [Fact]
        public async Task On_Exception_It_Gracefully_Stops()
        {
            _mockTimeService.Setup(t => t.DateTimeOffsetNow()).Returns(DateTimeOffset.Parse("02:30:00"));
            _mockFileInfoService.Setup(f => f.GetLastWriteTime(ScheduledTasksService.OK_FILE_NAME)).Returns(DateTime.Parse("02:00:00").AddDays(-1).ToUniversalTime());
            _services.AddSingleton(TestUtils.GetAppConfiguration());
            
            using var cts = new CancellationTokenSource();
            cts.Cancel();
            var cleanupService = new ScheduledTasksService(_services.BuildServiceProvider());
            await cleanupService.StartAsync(cts.Token);
            await Task.Delay(TimeSpan.FromSeconds(1));

            _mockLogger.Verify(l => l.Warning("Waiting for {time} before executing cleanup task...", It.IsAny<TimeSpan>()), Times.Never());
            _mockLogger.Verify(l => l.Error(It.IsAny<OperationCanceledException>(), "Failed at least one cleanup task. Application will continue."), Times.Once());
        }

        [Fact]
        public async Task Happy_Day_It_Runs_Successfully()
        {
            var config = TestUtils.GetAppConfiguration();
            _services.AddSingleton(config);

            var now = DateTimeOffset.Parse("02:30:00");
            _mockTimeService.Setup(t => t.DateTimeOffsetNow()).Returns(now);
            _mockTimeService.Setup(t => t.DateTimeUtcNow()).Returns(now.UtcDateTime);
            _mockFileInfoService.Setup(f => f.GetLastWriteTime(ScheduledTasksService.OK_FILE_NAME)).Returns(DateTime.Parse("02:00:00").AddDays(-1).ToUniversalTime());
            _mockWritingToolsService.Setup(w => w.DeleteOrphanedFiles()).ReturnsAsync((string.Empty, true));
            var postDtos = new List<PostDto>();
            var recycleBinItems = new List<PhpbbRecycleBin>();
            for (var i = 0; i < RandomValue.Int(15, 5); i++)
            {
                var item = RandomValue.Object<PhpbbRecycleBin>();
                if (item.Type == RecycleBinItemType.Post)
                {
                    var postDto = RandomValue.Object<PostDto>(new RandomValueSettings { IncludeNullAsPossibleValueForNullables = true });
                    postDto.PostEditTime = CustomRandomValue.UnixTimeStamp();
                    postDto.PostTime = CustomRandomValue.UnixTimeStamp();
                    if (postDto.Reports is not null)
                    {
                        var reports = postDto.Reports.ToList();
                        reports.ForEach(report => report.ReportTime = CustomRandomValue.UnixTimeStamp());
                        postDto.Reports = reports;
                    }
                    foreach (var a in postDto.Attachments.EmptyIfNull())
                    {
                        _mockStorageService.Setup(s => s.DeleteFile(a.PhysicalFileName, false)).Returns(true);
                    }
                    postDtos.Add(postDto); 
                    item.Content = await CompressionUtility.CompressObject(postDto);
                }
                recycleBinItems.Add(item);
            }
            _mockSqlExecuter
                .Setup(s => s.QueryAsync<PhpbbRecycleBin>(
                    It.Is<string>(sql => TestUtils.MatchesSqlStatement(sql, SqlStatements.SelectFromRecycleBin)),
                    It.IsAny<object>()))
                .ReturnsAsync(recycleBinItems);

            var operationLogs = RandomValue.List<PhpbbLog>();
            _mockSqlExecuter
                .Setup(s => s.QueryAsync<PhpbbLog>(
                    It.Is<string>(sql => TestUtils.MatchesSqlStatement(sql, SqlStatements.SelectFromOperationLogs)),
                    It.IsAny<object>()))
                .ReturnsAsync(operationLogs);

            var cleanupService = new ScheduledTasksService(_services.BuildServiceProvider());
            await cleanupService.StartAsync(CancellationToken.None);
            await Task.Delay(TimeSpan.FromSeconds(2));

            _mockLogger.Verify(l => l.Warning("Waiting for {time} before executing cleanup task...", It.IsAny<TimeSpan>()), Times.Never());
            foreach (var f in postDtos.SelectMany(p => p.Attachments?.Where(a => !string.IsNullOrWhiteSpace(a.PhysicalFileName)).Select(a => a.PhysicalFileName!) ?? Enumerable.Empty<string>()))
            {
                _mockStorageService.Verify(s => s.DeleteFile(f, false), Times.Once());
            }
            _mockSqlExecuter.Verify(
                s => s.ExecuteAsync(
                    It.Is<string>(sql => TestUtils.MatchesSqlStatement(sql, SqlStatements.DeleteFromRecycleBin)),
                    It.Is<IEnumerable<PhpbbRecycleBin>?>(items => items!.All(i => recycleBinItems.Any(ri => TestUtils.MatchesSqlParameters(i, ri))))),
                Times.Once());
            _mockSqlExecuter.Verify(
                s => s.ExecuteAsync(
                    It.Is<string>(sql => TestUtils.MatchesSqlStatement(sql, SqlStatements.UpdateAttachments)),
                    It.Is<object>(p => TestUtils.MatchesSqlParameters(
                        p,
                        new
                        {
                            now = now.UtcDateTime.ToUnixTimestamp(),
                            retention = config.GetObject<TimeSpan>("RecycleBinRetentionTime").TotalSeconds
                        }))),
                Times.Once());
            _mockSqlExecuter.Verify(
                s => s.ExecuteAsync(
                    It.Is<string>(sql => TestUtils.MatchesSqlStatement(sql, SqlStatements.UpdatePostsWithWrongForumId)),
                    null),
                Times.Once());
            _mockSqlExecuter.Verify(
                s => s.ExecuteAsync(
                    It.Is<string>(sql => TestUtils.MatchesSqlStatement(sql, SqlStatements.UpdateForumsWithWrongLastPost)),
                    It.Is<object>(p => TestUtils.MatchesSqlParameters(
                        p,
                        new
                        {
                            Constants.ANONYMOUS_USER_ID,
                            Constants.ANONYMOUS_USER_NAME,
                            Constants.DEFAULT_USER_COLOR
                        }))),
                Times.Once());
            _mockSqlExecuter.Verify(
                s => s.ExecuteAsync(
                    It.Is<string>(sql => TestUtils.MatchesSqlStatement(sql, SqlStatements.UpdateTopicsWithWrongLastPost)),
                    It.Is<object>(p => TestUtils.MatchesSqlParameters(
                        p,
                        new
                        {
                            Constants.ANONYMOUS_USER_ID,
                            Constants.ANONYMOUS_USER_NAME,
                            Constants.DEFAULT_USER_COLOR
                        }))),
                Times.Once());
            _mockSqlExecuter.Verify(
                s => s.ExecuteAsync(
                    It.Is<string>(sql => TestUtils.MatchesSqlStatement(sql, SqlStatements.DeleteFromOperationLogs)),
                    It.Is<object>(p => TestUtils.MatchesSqlParameters(
                        p,
                        new
                        {
                            ids = operationLogs.Select(l => l.LogId).DefaultIfEmpty()
                        }))),
                Times.Once());
            _mockWritingToolsService.Verify(w => w.DeleteOrphanedFiles(), Times.Once());
        }

        class SqlStatements
        {
            internal const string SelectFromRecycleBin = 
                "SELECT * FROM phpbb_recycle_bin WHERE @now - delete_time > @retention";

            internal const string SelectFromOperationLogs = 
                "SELECT * FROM phpbb_log WHERE @now - log_time > @retention";

            internal const string DeleteFromRecycleBin = 
                "DELETE FROM phpbb_recycle_bin WHERE type = @type AND id = @id";
            
            internal const string UpdateAttachments =
                @"UPDATE phpbb_attachments a
                    LEFT JOIN phpbb_posts p ON a.post_msg_id = p.post_id
                     SET a.is_orphan = 1
                   WHERE p.post_id IS NULL AND @now - a.filetime > @retention AND a.is_orphan = 0";
            
            internal const string UpdatePostsWithWrongForumId =
                @"UPDATE phpbb_posts p
                    JOIN phpbb_topics t ON p.topic_id = t.topic_id
                     SET p.forum_id = t.forum_id
                   WHERE p.forum_id <> t.forum_id";
            
            internal const string UpdateForumsWithWrongLastPost =
                @"UPDATE phpbb_forums f
                    JOIN (
                        WITH maxes AS (
	                        SELECT forum_id, MAX(post_time) AS post_time
	                         FROM phpbb_posts
	                        GROUP BY forum_id
					    )
                        SELECT DISTINCT p.*
						  FROM phpbb_posts p
						  JOIN maxes m ON p.forum_id = m.forum_id AND p.post_time = m.post_time
					) lp ON f.forum_id = lp.forum_id
                  LEFT JOIN phpbb_users u ON lp.poster_id = u.user_id
                   SET f.forum_last_post_id = lp.post_id,
	                   f.forum_last_poster_id = COALESCE(u.user_id, @ANONYMOUS_USER_ID),
                       f.forum_last_post_subject = lp.post_subject,
                       f.forum_last_post_time = lp.post_time,
                       f.forum_last_poster_name = COALESCE(u.username, @ANONYMOUS_USER_NAME),
                       f.forum_last_poster_colour = COALESCE(u.user_colour, @DEFAULT_USER_COLOR)
                 WHERE lp.post_id <> f.forum_last_post_id";

            internal const string UpdateTopicsWithWrongLastPost =
                @"UPDATE phpbb_topics t
                    JOIN (
                        WITH maxes AS (
	                      SELECT topic_id, MAX(post_time) AS post_time
		                    FROM phpbb_posts
		                   GROUP BY topic_id
                        )
	                    SELECT DISTINCT p.*
	                        FROM phpbb_posts p
	                        JOIN maxes m ON p.topic_id = m.topic_id AND p.post_time = m.post_time
                    ) lp ON t.topic_id = lp.topic_id
                    JOIN (
                        WITH mins AS (
	                        SELECT topic_id, MIN(post_time) AS post_time
	                          FROM phpbb_posts
	                         GROUP BY topic_id
                        )
	                    SELECT DISTINCT p.*
	                        FROM phpbb_posts p
	                        JOIN mins m ON p.topic_id = m.topic_id AND p.post_time = m.post_time
                    ) fp ON t.topic_id = fp.topic_id
                  LEFT JOIN phpbb_users lpu ON lp.poster_id = lpu.user_id
                  LEFT JOIN phpbb_users fpu ON fp.poster_id = fpu.user_id
                   SET t.topic_last_post_id = lp.post_id,
	                   t.topic_last_poster_id = COALESCE(lpu.user_id, @ANONYMOUS_USER_ID),
                       t.topic_last_post_subject = lp.post_subject,
                       t.topic_last_post_time = lp.post_time,
                       t.topic_last_poster_name = COALESCE(lpu.username, @ANONYMOUS_USER_NAME),
                       t.topic_last_poster_colour = COALESCE(lpu.user_colour, @DEFAULT_USER_COLOR),
                       t.topic_first_post_id = fp.post_id,
                       t.topic_first_poster_name = COALESCE(fpu.username, @ANONYMOUS_USER_NAME),
                       t.topic_first_poster_colour = COALESCE(fpu.user_colour, @DEFAULT_USER_COLOR)
                 WHERE lp.post_id <> t.topic_last_post_id OR fp.post_id <> t.topic_first_post_id";

            internal const string DeleteFromOperationLogs =
                "DELETE FROM phpbb_log WHERE log_id IN @ids";
        }
    }
}
