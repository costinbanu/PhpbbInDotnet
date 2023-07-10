using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PhpbbInDotnet.RecurringTasks.Tasks;
using PhpbbInDotnet.Services.Locks;
using PhpbbInDotnet.Services.Storage;
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
			var lockingService = scope.ServiceProvider.GetRequiredService<ILockingService>();

			bool lockAcquiredSuccessfully = false;
			bool shouldUpdateControlFile = false;
			string? lockId = null;

			try
			{
				var timeToWait = await schedulingService.GetTimeToWaitUntilRunIsAllowed();
				if (timeToWait > TimeSpan.Zero)
				{
					logger.Warning("Waiting for {time} before executing recurring tasks...", timeToWait);
					stoppingToken.WaitHandle.WaitOne(timeToWait);
				}

				stoppingToken.ThrowIfCancellationRequested();

				(lockAcquiredSuccessfully, lockId) = await lockingService.AcquireNamedLock(ControlFileName);
				if (lockAcquiredSuccessfully)
				{
					logger.Warning("Running recurring tasks on instance {name}.", Environment.MachineName);

                    await Task.WhenAll(scope.ServiceProvider.GetServices<IRecurringTask>().Select(t => t.ExecuteAsync(stoppingToken)));

					logger.Warning("All recurring tasks executed successfully on instance {name}.", Environment.MachineName);
					shouldUpdateControlFile = true;
				}
				else
				{
					logger.Warning("Will not execute recurring tasks on instance {name}. Another instance will handle this.", Environment.MachineName);
					return;
				}
			}
			catch (Exception ex)
			{
				logger.Error(ex, "An error occurred while running recurring tasks; rest of the application will continue.");
			}
			finally
			{
				if (lockAcquiredSuccessfully)
				{
					await lockingService.ReleaseNamedLock(ControlFileName, lockId!);
					if (shouldUpdateControlFile)
					{
						await storageService.WriteAllTextToFile(ControlFileName, $"Completed at {DateTime.UtcNow:u}.");
					}
                }
			}
		}

		public override async Task StopAsync(CancellationToken cancellationToken)
		{
			await base.StopAsync(cancellationToken);
			await base.StartAsync(cancellationToken);
		}
	}
}
