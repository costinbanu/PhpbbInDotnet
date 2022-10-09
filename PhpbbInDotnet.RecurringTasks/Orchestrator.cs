using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace PhpbbInDotnet.RecurringTasks
{
	sealed class Orchestrator : BackgroundService
	{
		readonly IServiceProvider _serviceProvider;
		readonly ISchedulingService _schedulingService;
		readonly Type[] _taskTypes;
		
		public Orchestrator(IServiceProvider serviceProvider, ISchedulingService schedulingService, params Type[] taskTypes)
		{
			_serviceProvider = serviceProvider;
			_schedulingService = schedulingService;
			_taskTypes = taskTypes;
		}

		protected override async Task ExecuteAsync(CancellationToken stoppingToken)
		{
            //hacky hack https://github.com/dotnet/runtime/issues/36063
            await Task.Yield();

            using var scope = _serviceProvider.CreateScope();
			var logger = scope.ServiceProvider.GetRequiredService<ILogger>();

            try
			{
				var timeToWait = _schedulingService.GetTimeToWaitUntilRunIsAllowed();
				if (timeToWait > TimeSpan.Zero)
				{
                    logger.Warning("Waiting for {time} before executing cleanup task...", timeToWait);
                    stoppingToken.WaitHandle.WaitOne(timeToWait);
                }

                var taskInstances = new List<BaseRecurringTask>();
				foreach (var taskType in _taskTypes)
				{
					var instance = ActivatorUtilities.CreateInstance(scope.ServiceProvider, taskType);
					if (instance is BaseRecurringTask recurringTask)
					{
						taskInstances.Add(recurringTask);
					}
					else
					{
						throw new ArgumentException($"{taskType.FullName} does not implement {nameof(BaseRecurringTask)}.");
					}
				}

				stoppingToken.ThrowIfCancellationRequested();

				await Task.WhenAll(taskInstances.Select(t => t.ExecuteAsync(stoppingToken)));
			}
			catch (Exception ex)
			{
				logger.Error("An error occurred while running recurring tasks; rest of the application will continue.", ex);
			}
		}

		public override async Task StopAsync(CancellationToken cancellationToken)
		{
			await base.StopAsync(cancellationToken);
			await base.StartAsync(cancellationToken);
		}
	}
}
