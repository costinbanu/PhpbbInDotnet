using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using PhpbbInDotnet.Database;
using PhpbbInDotnet.Objects.Configuration;
using PhpbbInDotnet.Utilities;
using Serilog;
using System;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace PhpbbInDotnet.Services.UnitTests
{
    public class CleanupServiceTests
    {
        readonly Mock<IDbConnection> _mockDbConnection;
        readonly Mock<IForumDbContext> _mockForumDbContext;
        readonly Mock<ICommonUtils> _mockUtils;
        readonly Mock<ILogger> _mockLogger;
        readonly Mock<IStorageService> _mockStorageService;
        readonly Mock<IWritingToolsService> _mockWritingToolsService;
        readonly Mock<ITimeService> _mockTimeService;
        
        readonly CleanupServiceOptions _options;

        readonly CleanupService _cleanupService;

        public CleanupServiceTests()
        {
            _mockDbConnection = new Mock<IDbConnection>();
            _mockForumDbContext = new Mock<IForumDbContext>();
            _mockForumDbContext.Setup(ctx => ctx.GetDbConnection()).Returns(_mockDbConnection.Object);
            _mockForumDbContext.Setup(ctx => ctx.GetDbConnectionAsync()).ReturnsAsync(_mockDbConnection.Object);
            _mockUtils = new Mock<ICommonUtils>();
            _mockLogger = new Mock<ILogger>();
            _mockStorageService = new Mock<IStorageService>();
            _mockWritingToolsService = new Mock<IWritingToolsService>();
            _mockTimeService = new Mock<ITimeService>();
            
            var config = new ConfigurationBuilder().AddJsonFile("appsettings.json", optional: false, reloadOnChange: true).Build();
            _options = config.GetObject<CleanupServiceOptions>("CleanupService");

            var services = new ServiceCollection();
            services.AddSingleton(config);
            services.AddScoped(_ => _mockForumDbContext.Object);
            services.AddSingleton(_mockUtils.Object);
            services.AddSingleton(_mockLogger.Object);
            services.AddScoped(_ => _mockStorageService.Object);
            services.AddScoped(_ => _mockWritingToolsService.Object);
            services.AddSingleton(_mockTimeService.Object);

            _cleanupService = new CleanupService(services.BuildServiceProvider());
        }

        [Theory]
        [InlineData(RunTime.BeforeAllowedTime)]
        public async Task Schedule_Is_Correct(RunTime runTime)
        {
            var token = new CancellationToken();
            var now = runTime switch
            {
                RunTime.BeforeAllowedTime => _options.MinimumAllowedRunTime.AddHours(-1),
                RunTime.DuringAllowedTime => _options.MinimumAllowedRunTime,
                RunTime.AfterAllowedTime => _options.MaximumAllowedRunTime.AddHours(1),
                _ => throw new ArgumentOutOfRangeException(nameof(runTime))
            };
            _mockTimeService.Setup(t => t.DateTimeOffsetNow()).Returns(now);

            await _cleanupService.StartAsync(token);

            _mockLogger.Verify(l => l.Warning("Waiting for {time} before executing cleanup task...", It.Is<TimeSpan>(t => t == TimeSpan.FromHours(1))), Times.Once());
        }

        public enum RunTime
        {
            BeforeAllowedTime,
            DuringAllowedTime,
            AfterAllowedTime
        }
    }
}