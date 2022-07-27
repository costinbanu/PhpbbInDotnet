using Microsoft.Extensions.DependencyInjection;
using Moq;
using PhpbbInDotnet.Database;
using Serilog;

namespace PhpbbInDotnet.Services.UnitTests.ScheduledTasksServiceTests
{
    public abstract class ScheduledTasksServiceTestsBase
    {
        protected readonly Mock<ISqlExecuter> _mockSqlExecuter;
        protected readonly Mock<IForumDbContext> _mockForumDbContext;
        protected readonly Mock<ILogger> _mockLogger;
        protected readonly Mock<IStorageService> _mockStorageService;
        protected readonly Mock<IWritingToolsService> _mockWritingToolsService;
        protected readonly Mock<ITimeService> _mockTimeService;
        protected readonly Mock<IFileInfoService> _mockFileInfoService;
        protected readonly IServiceCollection _services;

        public ScheduledTasksServiceTestsBase()
        {
            _mockSqlExecuter = new Mock<ISqlExecuter>();
            _mockForumDbContext = new Mock<IForumDbContext>();
            _mockForumDbContext.Setup(ctx => ctx.GetSqlExecuter()).Returns(_mockSqlExecuter.Object);
            _mockForumDbContext.Setup(ctx => ctx.GetSqlExecuterAsync()).ReturnsAsync(_mockSqlExecuter.Object);
            _mockLogger = new Mock<ILogger>();
            _mockStorageService = new Mock<IStorageService>();
            _mockWritingToolsService = new Mock<IWritingToolsService>();
            _mockTimeService = new Mock<ITimeService>();
            _mockFileInfoService = new Mock<IFileInfoService>();

            _services = new ServiceCollection();
            _services.AddScoped(_ => _mockForumDbContext.Object);
            _services.AddSingleton(_mockLogger.Object);
            _services.AddScoped(_ => _mockStorageService.Object);
            _services.AddScoped(_ => _mockWritingToolsService.Object);
            _services.AddSingleton(_mockTimeService.Object);
            _services.AddSingleton(_mockFileInfoService.Object);
        }
    }
}
