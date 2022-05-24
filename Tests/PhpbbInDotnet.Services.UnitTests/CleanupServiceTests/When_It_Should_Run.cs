using Microsoft.Extensions.DependencyInjection;
using Moq;
using PhpbbInDotnet.Database.Entities;
using PhpbbInDotnet.Objects;
using PhpbbInDotnet.Services.UnitTests.Utils;
using PhpbbInDotnet.Utilities.Core;
using RandomTestValues;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace PhpbbInDotnet.Services.UnitTests.CleanupServiceTests
{
    public class When_It_Should_Run : CleanupServiceTestsBase
    {
        [Fact]
        public async Task On_Exception_It_Gracefully_Stops()
        {
            _mockTimeService.Setup(t => t.DateTimeOffsetNow()).Returns(DateTimeOffset.Parse("02:30:00"));
            _mockFileInfoService.Setup(f => f.GetLastWriteTime(CleanupService.OK_FILE_NAME)).Returns(DateTime.Parse("02:00:00").AddDays(-1).ToUniversalTime());
            _services.AddSingleton(TestUtils.GetAppConfiguration());
            using var cts = new CancellationTokenSource();
            cts.Cancel();
            var cleanupService = new CleanupService(_services.BuildServiceProvider());

            await cleanupService.StartAsync(cts.Token);
            await Task.Delay(TimeSpan.FromSeconds(1));

            _mockLogger.Verify(l => l.Warning("Waiting for {time} before executing cleanup task...", It.IsAny<TimeSpan>()), Times.Never());
            _mockLogger.Verify(l => l.Error(It.IsAny<OperationCanceledException>(), "Failed at least one cleanup task. Application will continue."), Times.Once());
        }

        [Fact]
        public async Task Happy_Day_It_Runs_Successfully()
        {
            _mockTimeService.Setup(t => t.DateTimeOffsetNow()).Returns(DateTimeOffset.Parse("02:30:00"));
            _mockFileInfoService.Setup(f => f.GetLastWriteTime(CleanupService.OK_FILE_NAME)).Returns(DateTime.Parse("02:00:00").AddDays(-1).ToUniversalTime());
            _services.AddSingleton(TestUtils.GetAppConfiguration());
            using var cts = new CancellationTokenSource();
            var postDtos = new List<PostDto>();
            var recycleBinItems = new List<PhpbbRecycleBin>();
            for (var i = 0; i < RandomValue.Int(15, 5); i++)
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
                postDtos.Add(postDto);
                var item = RandomValue.Object<PhpbbRecycleBin>();
                item.Content = await CompressionUtils.CompressObject(postDto);
                recycleBinItems.Add(item);
            }
            
            _mockSqlExecuter
                .Setup(c => c.QueryAsync<PhpbbRecycleBin>(It.IsAny<string>(), It.IsAny<object>()))
                .ReturnsAsync(recycleBinItems);

            var cleanupService = new CleanupService(_services.BuildServiceProvider());
            await cleanupService.StartAsync(cts.Token);
            await Task.Delay(TimeSpan.FromSeconds(5));
            cts.Cancel();

            _mockLogger.Verify(l => l.Warning("Waiting for {time} before executing cleanup task...", It.IsAny<TimeSpan>()), Times.Never());
            _mockLogger.Verify(l => l.Error(It.IsAny<OperationCanceledException>(), "Failed at least one cleanup task. Application will continue."), Times.Once());
            var fileNames = new HashSet<string>(postDtos.SelectMany(p => p.Attachments?.Where(a => !string.IsNullOrWhiteSpace(a.PhysicalFileName)).Select(a => a.PhysicalFileName!) ?? Enumerable.Empty<string>()));
            _mockStorageService.Verify(
                s => s.DeleteFile(It.Is<string>(f => fileNames.Contains(f)), false), 
                Times.Exactly(fileNames.Count));
            _mockSqlExecuter.Verify(
                s => s.ExecuteAsync(
                    "DELETE FROM phpbb_recycle_bin WHERE type = @type AND id = @id",
                    It.Is<object>(p => VerifyDeleteStatementParams(p, recycleBinItems))),
                Times.Exactly(recycleBinItems.Count));
        }

        bool VerifyDeleteStatementParams(object param, List<PhpbbRecycleBin> recycleBinItems)
        {
            var type = ((dynamic)param).Type;
            var id = ((dynamic)param).Id;
            var byId = recycleBinItems.FirstOrDefault(i => i.Id == id);
            return byId is not null && byId.Type == type;
        }
    }
}
