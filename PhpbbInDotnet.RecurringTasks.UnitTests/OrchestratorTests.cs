using Microsoft.Extensions.DependencyInjection;
using Moq;
using PhpbbInDotnet.RecurringTasks.Tasks;
using PhpbbInDotnet.Services;
using Serilog;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace PhpbbInDotnet.RecurringTasks.UnitTests
{
    public class OrchestratorTests
    {
        readonly Mock<ILogger> _mockLogger;
        readonly Mock<IStorageService> _mockStorageService;
        readonly IServiceCollection _services;

        public OrchestratorTests()
        {
            _mockLogger = new Mock<ILogger>();
            _mockStorageService = new Mock<IStorageService>();
            var mockSchedulingService = new Mock<ISchedulingService>();
            mockSchedulingService.Setup(s => s.GetTimeToWaitUntilRunIsAllowed()).Returns(TimeSpan.Zero);

            _services = new ServiceCollection();
            _services.AddSingleton(_mockLogger.Object);
            _services.AddScoped(_ => _mockStorageService.Object);
            _services.AddSingleton(mockSchedulingService.Object);
        }

        [Fact]
        public async Task On_Task_Cancellation_It_Gracefully_Stops()
        {
            using var cts = new CancellationTokenSource();
            cts.Cancel();
            var orchestrator = new Orchestrator(_services.BuildServiceProvider(), typeof(FakeForumsAndTopicsSynchronizer));
            await orchestrator.StartAsync(cts.Token);
            await Task.Delay(TimeSpan.FromSeconds(1));

            _mockLogger.Verify(
                l => l.Error(
                    It.Is<OperationCanceledException>(e => e.Message == "The operation was canceled."), 
                    "An error occurred while running recurring tasks; rest of the application will continue."), 
                Times.Once());
        }

        [Fact]
        public async Task On_Wrong_Task_Types_It_Gracefully_Stops()
        {
            var orchestrator = new Orchestrator(_services.BuildServiceProvider(), typeof(object));
            await orchestrator.StartAsync(CancellationToken.None);
            await Task.Delay(TimeSpan.FromSeconds(1));

            _mockLogger.Verify(
                l => l.Error(
                    It.Is<ArgumentException>(e => e.Message == $"System.Object does not implement {nameof(IRecurringTask)}."), 
                    "An error occurred while running recurring tasks; rest of the application will continue."), 
                Times.Once());
        }

        [Fact]
        public async Task Happy_Day_It_Runs_Successfully()
        {
            var counter = new CallCounter();
            _services.AddSingleton(counter);
            var orchestrator = new Orchestrator(
                _services.BuildServiceProvider(),
                typeof(FakeForumsAndTopicsSynchronizer),
                typeof(FakeLogCleaner),
                typeof(FakeOrphanFilesCleaner),
                typeof(FakeRecycleBinCleaner),
                typeof(FakeSiteMapGenerator));

            await orchestrator.StartAsync(CancellationToken.None);
            await Task.Delay(TimeSpan.FromSeconds(1));

            Assert.Equal(1, Volatile.Read(ref counter.ForumsAndTopicsSynchronizerCalls));
            Assert.Equal(1, Volatile.Read(ref counter.LogCleanerCalls));
            Assert.Equal(1, Volatile.Read(ref counter.OrphanFileCleanerCalls));
            Assert.Equal(1, Volatile.Read(ref counter.RecycleBinCleanerCalls));
            Assert.Equal(1, Volatile.Read(ref counter.SiteMapGeneratorCalls));
            _mockStorageService.Verify(s => s.WriteAllTextToFile(Orchestrator.ControlFileName, string.Empty), Times.Once());
        }

        class CallCounter 
        {
            internal int ForumsAndTopicsSynchronizerCalls = 0;
            internal int LogCleanerCalls = 0;
            internal int OrphanFileCleanerCalls = 0;
            internal int RecycleBinCleanerCalls = 0;
            internal int SiteMapGeneratorCalls = 0;
        }

        class FakeForumsAndTopicsSynchronizer : IRecurringTask
        {
            readonly CallCounter _callCounter;

            public FakeForumsAndTopicsSynchronizer(CallCounter callCounter)
            {
                _callCounter = callCounter;
            }

            public Task ExecuteAsync(CancellationToken stoppingToken)
            {
                Interlocked.Increment(ref _callCounter.ForumsAndTopicsSynchronizerCalls);
                return Task.CompletedTask;
            }
        }

        class FakeLogCleaner : IRecurringTask
        {
            readonly CallCounter _callCounter;

            public FakeLogCleaner(CallCounter callCounter)
            {
                _callCounter = callCounter;
            }

            public Task ExecuteAsync(CancellationToken stoppingToken)
            {
                Interlocked.Increment(ref _callCounter.LogCleanerCalls);
                return Task.CompletedTask;
            }
        }

        class FakeOrphanFilesCleaner : IRecurringTask
        {
            readonly CallCounter _callCounter;

            public FakeOrphanFilesCleaner(CallCounter callCounter)
            {
                _callCounter = callCounter;
            }

            public Task ExecuteAsync(CancellationToken stoppingToken)
            {
                Interlocked.Increment(ref _callCounter.OrphanFileCleanerCalls);
                return Task.CompletedTask;
            }
        }

        class FakeRecycleBinCleaner : IRecurringTask
        {
            readonly CallCounter _callCounter;

            public FakeRecycleBinCleaner(CallCounter callCounter)
            {
                _callCounter = callCounter;
            }

            public Task ExecuteAsync(CancellationToken stoppingToken)
            {
                Interlocked.Increment(ref _callCounter.RecycleBinCleanerCalls);
                return Task.CompletedTask;
            }
        }

        class FakeSiteMapGenerator : IRecurringTask
        {
            readonly CallCounter _callCounter;

            public FakeSiteMapGenerator(CallCounter callCounter)
            {
                _callCounter = callCounter;
            }

            public Task ExecuteAsync(CancellationToken stoppingToken)
            {
                Interlocked.Increment(ref _callCounter.SiteMapGeneratorCalls);
                return Task.CompletedTask;
            }
        }
    }
}
