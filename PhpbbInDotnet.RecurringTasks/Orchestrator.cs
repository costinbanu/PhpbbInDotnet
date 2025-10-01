using Coravel.Invocable;
using Microsoft.Extensions.Configuration;
using PhpbbInDotnet.RecurringTasks.Tasks;
using PhpbbInDotnet.Services;
using PhpbbInDotnet.Services.Locks;
using PhpbbInDotnet.Services.Storage;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PhpbbInDotnet.RecurringTasks
{
    sealed class Orchestrator : IInvocable, ICancellableInvocable
    {
		readonly IEnumerable<IRecurringTask> _tasks;
		readonly ILogger _logger;
		readonly IStorageService _storageService;
		readonly ILockingService _lockingService;
        readonly ITimeService _timeService;
        private readonly IConfiguration _configuration;
        internal const string ControlFileName = "RecurringTasks.ok";

        public CancellationToken CancellationToken { get; set; }

        public Orchestrator(IEnumerable<IRecurringTask> tasks, ILogger logger, IStorageService storageService, ILockingService lockingService, ITimeService timeService, IConfiguration configuration)
		{
			_tasks = tasks;
			_logger = logger;
			_storageService = storageService;
            _lockingService = lockingService;
			_timeService = timeService;
			_configuration = configuration;
        }

		public Task Invoke()
		{
			_ = ExecuteTask();
			return Task.CompletedTask;
		}

		private async Task ExecuteTask()
		{
			bool lockAcquiredSuccessfully = false;
			bool shouldUpdateControlFile = false;
			string? lockId = null;
			TimeSpan? duration = null;

			var configComputerName = _configuration.GetValue<string?>("COMPUTERNAME");
			var computerName =  string.IsNullOrWhiteSpace(configComputerName) ? Environment.MachineName : configComputerName;

			try
			{
				(lockAcquiredSuccessfully, lockId, duration) = await _lockingService.AcquireNamedLock(ControlFileName, CancellationToken);
				if (lockAcquiredSuccessfully)
				{
					_logger.Warning("Running recurring tasks on instance {name} with lock id {lockId}.", computerName, lockId);

					using var lockRenewalCancellation = new CancellationTokenSource(TimeSpan.FromMinutes(10));
                    using var combinedCancellation = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken, lockRenewalCancellation.Token);
                    var lockRenewalTask = RenewLockAsync(lockId!, duration!.Value, combinedCancellation.Token);
                    
					await Task.WhenAll(_tasks.Select(t => t.ExecuteAsync(CancellationToken)));
                    shouldUpdateControlFile = true;
                    
					lockRenewalCancellation.Cancel();
					await lockRenewalTask;

					_logger.Warning("All recurring tasks executed successfully on instance {name} with lock id {lockId}.", computerName, lockId);
				}
				else
				{
					_logger.Warning("Will not execute recurring tasks on instance {name}. Another instance will handle this.", computerName);
					return;
				}
			}
			catch (Exception ex)
			{
				_logger.Error(ex, "An error occurred while running recurring tasks; rest of the application will continue.");
			}
			finally
			{
				if (lockAcquiredSuccessfully)
				{
					await _lockingService.ReleaseNamedLock(ControlFileName, lockId!, CancellationToken);
					if (shouldUpdateControlFile)
					{
						await _storageService.WriteAllTextToFile(ControlFileName, $"Completed at {_timeService.DateTimeUtcNow():u} on instance {computerName}.");
					}
                }
			}
		}

		private async Task RenewLockAsync(string lockId, TimeSpan duration, CancellationToken cancellationToken)
		{
			var sleepTime = duration - TimeSpan.FromSeconds(2);
			bool shouldContinue;
			do
			{
				try
				{
					await Task.Delay(sleepTime, cancellationToken);
					shouldContinue = await _lockingService.RenewNamedLock(ControlFileName, lockId, cancellationToken);
					_logger.Warning("Renewed lock {id} for another {time}", lockId, sleepTime);
				}
				catch
				{
					shouldContinue = false;
				}
			}
			while (shouldContinue);

			_logger.Information("Stopped renewing lock {id}", lockId);
		}
	}
}
