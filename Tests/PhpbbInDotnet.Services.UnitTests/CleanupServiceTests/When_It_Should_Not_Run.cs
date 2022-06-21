using Microsoft.Extensions.DependencyInjection;
using Moq;
using PhpbbInDotnet.Objects.Configuration;
using PhpbbInDotnet.Services.UnitTests.Utils;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace PhpbbInDotnet.Services.UnitTests.CleanupServiceTests
{
    public class When_It_Should_Not_Run : CleanupServiceTestsBase
    {
        protected async Task RunTest(CleanupServiceOptions options, DateTime? lastRun, DateTimeOffset now, TimeSpan waitTime)
        {
            using var cts = new CancellationTokenSource();
            _mockTimeService.Setup(t => t.DateTimeOffsetNow()).Returns(now);
            _mockFileInfoService.Setup(f => f.GetLastWriteTime(ScheduledTasksService.OK_FILE_NAME)).Returns(lastRun);
            _services.AddSingleton(TestUtils.GetAppConfiguration(opts => opts.CleanupService = options));
            var cleanupService = new ScheduledTasksService(_services.BuildServiceProvider());

            await cleanupService.StartAsync(cts.Token);
            await Task.Delay(TimeSpan.FromSeconds(1));
            cts.Cancel();

            _mockLogger.Verify(l => l.Warning("Waiting for {time} before executing cleanup task...", It.Is<TimeSpan>(t => t == waitTime)), Times.Once());
        }

        public class And_Has_Ran_Before : When_It_Should_Not_Run
        {
            public static TheoryData<CleanupServiceOptions, DateTime?, DateTimeOffset, TimeSpan> ScheduleTestData
                => new()
                {
                    {
                        new CleanupServiceOptions
                        {
                            MinimumAllowedRunTime = DateTimeOffset.Parse("02:00:00"),
                            MaximumAllowedRunTime = DateTimeOffset.Parse("03:00:00"),
                            Interval = TimeSpan.FromDays(1)
                        },
                        DateTime.Parse("01:00:00").AddDays(-1).ToUniversalTime(),
                        DateTimeOffset.Parse("01:00:00"),
                        TimeSpan.FromHours(1)
                    },
                    {
                        new CleanupServiceOptions
                        {
                            MinimumAllowedRunTime = DateTimeOffset.Parse("02:00:00"),
                            MaximumAllowedRunTime = DateTimeOffset.Parse("03:00:00"),
                            Interval = TimeSpan.FromDays(1)
                        },
                        DateTime.Parse("01:00:00").AddDays(-1).ToUniversalTime(),
                        DateTimeOffset.Parse("04:00:00"),
                        TimeSpan.FromHours(22)
                    },
                    {
                        new CleanupServiceOptions
                        {
                            MinimumAllowedRunTime = DateTimeOffset.Parse("02:00:00"),
                            MaximumAllowedRunTime = DateTimeOffset.Parse("03:00:00"),
                            Interval = TimeSpan.FromDays(5)
                        },
                        DateTime.Parse("01:00:00").AddDays(-3).ToUniversalTime(),
                        DateTimeOffset.Parse("01:00:00"),
                        TimeSpan.Parse("2.01:00:00")
                    },
                    {
                        new CleanupServiceOptions
                        {
                            MinimumAllowedRunTime = DateTimeOffset.Parse("02:00:00"),
                            MaximumAllowedRunTime = DateTimeOffset.Parse("03:00:00"),
                            Interval = TimeSpan.FromDays(1)
                        },
                        DateTime.Parse("01:00:00").AddDays(-3).ToUniversalTime(),
                        DateTimeOffset.Parse("04:00:00"),
                        TimeSpan.Parse("0.22:00:00")
                    },
                    {
                        new CleanupServiceOptions
                        {
                            MinimumAllowedRunTime = DateTimeOffset.Parse("02:00:00"),
                            MaximumAllowedRunTime = DateTimeOffset.Parse("03:00:00"),
                            Interval = TimeSpan.FromDays(5)
                        },
                        DateTime.Parse("01:00:00").AddDays(-3).ToUniversalTime(),
                        DateTimeOffset.Parse("04:00:00"),
                        TimeSpan.Parse("1.22:00:00")
                    },
                };

            [Theory]
            [MemberData(nameof(ScheduleTestData))]
            public Task Then_Schedule_Is_Correct(CleanupServiceOptions options, DateTime? lastRun, DateTimeOffset now, TimeSpan waitTime)
                => RunTest(options, lastRun, now, waitTime);
        }

        public class And_Has_Not_Ran_Before : When_It_Should_Not_Run
        {
            public static TheoryData<CleanupServiceOptions, DateTime?, DateTimeOffset, TimeSpan> ScheduleTestData
                => new()
                {
                    {
                        new CleanupServiceOptions
                        {
                            MinimumAllowedRunTime = DateTimeOffset.Parse("02:00:00"),
                            MaximumAllowedRunTime = DateTimeOffset.Parse("03:00:00"),
                            Interval = TimeSpan.FromDays(1)
                        },
                        null,
                        DateTimeOffset.Parse("01:00:00"),
                        TimeSpan.FromHours(1)
                    },
                    {
                        new CleanupServiceOptions
                        {
                            MinimumAllowedRunTime = DateTimeOffset.Parse("02:00:00"),
                            MaximumAllowedRunTime = DateTimeOffset.Parse("03:00:00"),
                            Interval = TimeSpan.FromDays(1)
                        },
                        null,
                        DateTimeOffset.Parse("04:00:00"),
                        TimeSpan.FromHours(22)
                    },
                    {
                        new CleanupServiceOptions
                        {
                            MinimumAllowedRunTime = DateTimeOffset.Parse("02:00:00"),
                            MaximumAllowedRunTime = DateTimeOffset.Parse("03:00:00"),
                            Interval = TimeSpan.FromDays(5)
                        },
                        null,
                        DateTimeOffset.Parse("01:00:00"),
                        TimeSpan.FromHours(1)
                    },
                    {
                        new CleanupServiceOptions
                        {
                            MinimumAllowedRunTime = DateTimeOffset.Parse("02:00:00"),
                            MaximumAllowedRunTime = DateTimeOffset.Parse("03:00:00"),
                            Interval = TimeSpan.FromDays(5)
                        },
                        null,
                        DateTimeOffset.Parse("04:00:00"),
                        TimeSpan.FromHours(22)
                    },
                };

            [Theory]
            [MemberData(nameof(ScheduleTestData))]
            public Task Then_Schedule_Is_Correct(CleanupServiceOptions options, DateTime? lastRun, DateTimeOffset now, TimeSpan waitTime)
                => RunTest(options, lastRun, now, waitTime);
        }
    }
}