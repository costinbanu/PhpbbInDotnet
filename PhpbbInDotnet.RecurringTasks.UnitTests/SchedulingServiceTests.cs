using Moq;
using PhpbbInDotnet.Objects.Configuration;
using PhpbbInDotnet.RecurringTasks.UnitTests.Utils;
using PhpbbInDotnet.Services;
using System;
using Xunit;

namespace PhpbbInDotnet.RecurringTasks.UnitTests
{
    public class SchedulingServiceTests
    {
        static ISchedulingService GetSchedulingService(DateTime? lastRun, DateTimeOffset now, Action<AppSettingsObject>? setupOptions = null)
        {
            var mockTimeService = new Mock<ITimeService>();
            mockTimeService.Setup(t => t.DateTimeOffsetNow()).Returns(now);
            var mockFileInfoService = new Mock<IFileInfoService>();
            mockFileInfoService.Setup(f => f.GetLastWriteTime(Orchestrator.ControlFileName)).Returns(lastRun);
            var testConfig = TestUtils.GetAppConfiguration(setupOptions);

            return new SchedulingService(mockTimeService.Object, mockFileInfoService.Object, testConfig);
        }

        public class When_It_Should_Not_Run : SchedulingServiceTests
        {
            static void RunTest(CleanupServiceOptions options, DateTime? lastRun, DateTimeOffset now, TimeSpan expected)
            {
                var schedulingService = GetSchedulingService(lastRun, now, opts => opts.CleanupService = options);

                var actual = schedulingService.GetTimeToWaitUntilRunIsAllowed();

                Assert.Equal(expected, actual);
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
                public void Then_Schedule_Is_Correct(CleanupServiceOptions options, DateTime? lastRun, DateTimeOffset now, TimeSpan waitTime)
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
                public void Then_Schedule_Is_Correct(CleanupServiceOptions options, DateTime? lastRun, DateTimeOffset now, TimeSpan waitTime)
                    => RunTest(options, lastRun, now, waitTime);
            }
        }

        public class When_It_Should_Run : SchedulingServiceTests
        {
            [Fact]
            public void Then_Wait_Time_Is_Zero()
            {
                var schedulingService = GetSchedulingService(DateTime.Parse("02:00:00").AddDays(-1).ToUniversalTime(), DateTimeOffset.Parse("02:30:00"));

                var actual = schedulingService.GetTimeToWaitUntilRunIsAllowed();

                Assert.Equal(TimeSpan.Zero, actual);
            }
        }
    }
}
