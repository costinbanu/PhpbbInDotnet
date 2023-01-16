using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PhpbbInDotnet.RecurringTasks.Tasks;
using PhpbbInDotnet.Services;
using Serilog;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PhpbbInDotnet.RecurringTasks
{
	sealed class Orchestrator : BackgroundService
	{
		readonly IServiceProvider _serviceProvider;

		internal const string ControlFileName = "RecurringTasks.ok";
		
		public Orchestrator(IServiceProvider serviceProvider)
		{
			_serviceProvider = serviceProvider;
		}

		protected override async Task ExecuteAsync(CancellationToken stoppingToken)
		{
            //hacky hack https://github.com/dotnet/runtime/issues/36063
            await Task.Yield();

            using var scope = _serviceProvider.CreateScope();
			var logger = scope.ServiceProvider.GetRequiredService<ILogger>();
			var storageService = scope.ServiceProvider.GetRequiredService<IStorageService>();
            var schedulingService = scope.ServiceProvider.GetRequiredService<ISchedulingService>();

            try
            {
				var timeToWait = schedulingService.GetTimeToWaitUntilRunIsAllowed();
				if (timeToWait > TimeSpan.Zero)
				{
                    logger.Warning("Waiting for {time} before executing recurring tasks...", timeToWait);
                    stoppingToken.WaitHandle.WaitOne(timeToWait);
                }

                stoppingToken.ThrowIfCancellationRequested();

				await Task.WhenAll(scope.ServiceProvider.GetServices<IRecurringTask>().Select(t => t.ExecuteAsync(stoppingToken)));

                storageService.WriteAllTextToFile(ControlFileName, string.Empty);
            }
			catch (Exception ex)
			{
				logger.Error(ex, "An error occurred while running recurring tasks; rest of the application will continue.");
			}
		}

		public override async Task StopAsync(CancellationToken cancellationToken)
		{
			await base.StopAsync(cancellationToken);
			await base.StartAsync(cancellationToken);
		}
	}
}
