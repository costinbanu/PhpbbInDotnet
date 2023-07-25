using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using PhpbbInDotnet.RecurringTasks.Tasks;
using PhpbbInDotnet.Services;
using PhpbbInDotnet.Services.Locks;
using PhpbbInDotnet.Services.Storage;
using Serilog;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace PhpbbInDotnet.RecurringTasks.UnitTests
{
    public class OrchestratorTests
    {
        readonly Mock<ILogger> _mockLogger;
        readonly Mock<IStorageService> _mockStorageService;
        readonly Mock<ILockingService> _mockLockingService;
        readonly Mock<ITimeService> _mockTimeService;
        readonly string _computerName;

        public OrchestratorTests()
        {
            _mockLogger = new Mock<ILogger>();
            _mockStorageService = new Mock<IStorageService>();
            _mockLockingService = new Mock<ILockingService>();
            _mockTimeService = new Mock<ITimeService>();
            _computerName = Guid.NewGuid().ToString();
        }

        IServiceCollection GetServices()
        {
            var services = new ServiceCollection();
            services.AddSingleton(_mockLogger.Object);
            services.AddScoped(_ => _mockStorageService.Object);
            services.AddScoped(_ => _mockLockingService.Object);
            services.AddScoped(_ => _mockTimeService.Object);
            services.AddScoped<Orchestrator>();

            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string> { ["COMPUTERNAME"] = _computerName }).Build();
            services.AddSingleton<IConfiguration>(_ => config);

            return services;
        }

        [Fact]
        public async Task On_Parallel_Run_It_Gracefully_Stops()
        {
            _mockLockingService.Setup(l => l.AcquireNamedLock(It.IsAny<string>())).ReturnsAsync((false, null));
            _mockLockingService.Setup(l => l.ReleaseNamedLock(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(true);

            var services = GetServices();
            services.AddSingleton<CallCounter>();
            services.AddScoped<IRecurringTask, FakeForumsAndTopicsSynchronizer>();

            using var cts = new CancellationTokenSource();
            cts.Cancel();

            var orchestrator = services.BuildServiceProvider().GetRequiredService<Orchestrator>();
            orchestrator.CancellationToken = cts.Token;
            await orchestrator.Invoke();
            await Task.Delay(TimeSpan.FromSeconds(1));

            _mockLogger.Verify(
                l => l.Warning(
                    It.Is<string>(m => m == "Will not execute recurring tasks on instance {name}. Another instance will handle this."),
                    It.Is<string>(parm => parm == _computerName)),
                Times.Once());
        }

        [Fact]
        public async Task On_Task_Cancellation_It_Gracefully_Stops()
        {
            _mockLockingService.Setup(l => l.AcquireNamedLock(It.IsAny<string>())).ReturnsAsync((true, Guid.NewGuid().ToString()));
            _mockLockingService.Setup(l => l.ReleaseNamedLock(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(true);

            var services = GetServices();
            services.AddSingleton<CallCounter>();
            services.AddScoped<IRecurringTask, FakeForumsAndTopicsSynchronizer>();

            using var cts = new CancellationTokenSource();
            cts.Cancel();

            var orchestrator = services.BuildServiceProvider().GetRequiredService<Orchestrator>();
            orchestrator.CancellationToken = cts.Token;
            await orchestrator.Invoke();
            await Task.Delay(TimeSpan.FromSeconds(1));

            _mockLogger.Verify(
                l => l.Error(
                    It.Is<OperationCanceledException>(e => e.Message == "The operation was canceled."), 
                    "An error occurred while running recurring tasks; rest of the application will continue."), 
                Times.Once());
        }

        [Fact]
        public async Task Happy_Day_It_Runs_Successfully()
        {
            var now = DateTime.UtcNow;
            _mockTimeService.Setup(t => t.DateTimeUtcNow()).Returns(now);
            _mockLockingService.Setup(l => l.AcquireNamedLock(It.IsAny<string>())).ReturnsAsync((true, Guid.NewGuid().ToString()));
            _mockLockingService.Setup(l => l.ReleaseNamedLock(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(true);

            var services = GetServices();
            var counter = new CallCounter();
            services.AddSingleton(counter);
            services.AddScoped<IRecurringTask, FakeForumsAndTopicsSynchronizer>();
            services.AddScoped<IRecurringTask, FakeLogCleaner>();
            services.AddScoped<IRecurringTask, FakeOrphanFilesCleaner>();
            services.AddScoped<IRecurringTask, FakeRecycleBinCleaner>();
            services.AddScoped<IRecurringTask, FakeSiteMapGenerator>();
            
            var orchestrator = services.BuildServiceProvider().GetRequiredService<Orchestrator>();
            orchestrator.CancellationToken = CancellationToken.None;
            await orchestrator.Invoke();
            await Task.Delay(TimeSpan.FromSeconds(1));

            Assert.Equal(1, Volatile.Read(ref counter.ForumsAndTopicsSynchronizerCalls));
            Assert.Equal(1, Volatile.Read(ref counter.LogCleanerCalls));
            Assert.Equal(1, Volatile.Read(ref counter.OrphanFileCleanerCalls));
            Assert.Equal(1, Volatile.Read(ref counter.RecycleBinCleanerCalls));
            Assert.Equal(1, Volatile.Read(ref counter.SiteMapGeneratorCalls));
            _mockStorageService.Verify(s => s.WriteAllTextToFile(Orchestrator.ControlFileName, $"Completed at {now:u} on instance {_computerName}."), Times.Once());
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
                stoppingToken.ThrowIfCancellationRequested();
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
                stoppingToken.ThrowIfCancellationRequested();
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
                stoppingToken.ThrowIfCancellationRequested();
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
                stoppingToken.ThrowIfCancellationRequested();
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
                stoppingToken.ThrowIfCancellationRequested();
                Interlocked.Increment(ref _callCounter.SiteMapGeneratorCalls);
                return Task.CompletedTask;
            }
        }
    }
}
