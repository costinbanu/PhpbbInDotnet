using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PhpbbInDotnet.RecurringTasks.Tasks;
using PhpbbInDotnet.Services;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PhpbbInDotnet.RecurringTasks
{
	sealed class Orchestrator : BackgroundService
	{
		readonly IServiceProvider _serviceProvider;
		readonly ISchedulingService _schedulingService;
		readonly Type[] _taskTypes;

		internal const string ControlFileName = "RecurringTasks.ok";
		
		public Orchestrator(IServiceProvider serviceProvider, params Type[] taskTypes)
		{
			_serviceProvider = serviceProvider;
			_schedulingService = serviceProvider.GetRequiredService<ISchedulingService>();
			_taskTypes = taskTypes;
		}

		protected override async Task ExecuteAsync(CancellationToken stoppingToken)
		{
            //hacky hack https://github.com/dotnet/runtime/issues/36063
            await Task.Yield();

            using var scope = _serviceProvider.CreateScope();
			var logger = scope.ServiceProvider.GetRequiredService<ILogger>();
			var storageService = scope.ServiceProvider.GetRequiredService<IStorageService>();

            try
			{
				var timeToWait = _schedulingService.GetTimeToWaitUntilRunIsAllowed();
				if (timeToWait > TimeSpan.Zero)
				{
                    logger.Warning("Waiting for {time} before executing cleanup task...", timeToWait);
                    stoppingToken.WaitHandle.WaitOne(timeToWait);
                }

                stoppingToken.ThrowIfCancellationRequested();

                var recurringTasks = new List<IRecurringTask>();
				foreach (var taskType in _taskTypes)
				{
					var instance = ActivatorUtilities.CreateInstance(scope.ServiceProvider, taskType);
					if (instance is IRecurringTask recurringTask)
					{
						recurringTasks.Add(recurringTask);
					}
					else
					{
						throw new ArgumentException($"{taskType.FullName} does not implement {nameof(IRecurringTask)}.");
					}
				}

				stoppingToken.ThrowIfCancellationRequested();

				await Task.WhenAll(recurringTasks.Select(t => t.ExecuteAsync(stoppingToken)));

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
