using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PhpbbInDotnet.Database;
using PhpbbInDotnet.Objects;
using PhpbbInDotnet.Utilities;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PhpbbInDotnet.Services
{
    public class DailyCleanupService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;

        public DailyCleanupService(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            using var scope = _serviceProvider.CreateScope();
            var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
            var dbContext = scope.ServiceProvider.GetRequiredService<ForumDbContext>();
            var utils = scope.ServiceProvider.GetRequiredService<CommonUtils>();
            var storageService = scope.ServiceProvider.GetRequiredService<StorageService>();

            var retention = config.GetObject<TimeSpan?>("RecycleBinRetentionTime") ?? TimeSpan.FromDays(7);
            var now = DateTime.UtcNow.ToUnixTimestamp();
            var toDelete = await (
                from rb in dbContext.PhpbbRecycleBin
                where now - rb.DeleteTime > retention.TotalSeconds
                select rb
            ).ToListAsync();

            if (!toDelete.Any())
            {
                return;
            }

            dbContext.PhpbbRecycleBin.RemoveRange(toDelete);

            var posts = await Task.WhenAll(
                from i in toDelete
                where i.Type == RecycleBinItemType.Post
                select utils.DecompressObject<PostDto>(i.Content)
            );

            //either stop now, before doing permanent deletions, either not at all
            stoppingToken.ThrowIfCancellationRequested();

            var deleteResults = from p in posts
                                where p?.Attachments?.Any() ?? false

                                from a in p.Attachments
                                where !string.IsNullOrWhiteSpace(a?.PhysicalFileName)

                                select storageService.DeleteFile(a.PhysicalFileName, false);

            if (deleteResults.Any(r => !r))
            {
                utils.HandleErrorAsWarning(
                    ex: new Exception("Not all attachments have been permanently deleted successfully."),
                    message: "Unexpected failure during daily cleanup"
                );
            }

            await dbContext.SaveChangesAsync(CancellationToken.None);
        }
    }
}
