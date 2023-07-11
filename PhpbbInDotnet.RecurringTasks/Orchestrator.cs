using Coravel.Invocable;
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

        internal const string ControlFileName = "RecurringTasks.ok";

        public CancellationToken CancellationToken { get; set; }

        public Orchestrator(IEnumerable<IRecurringTask> tasks, ILogger logger, IStorageService storageService, ILockingService lockingService, ITimeService timeService)
		{
			_tasks = tasks;
			_logger = logger;
			_storageService = storageService;
            _lockingService = lockingService;
			_timeService = timeService;
        }

        public async Task Invoke()
		{
            //hacky hack https://github.com/dotnet/runtime/issues/36063
            await Task.Yield();

			bool lockAcquiredSuccessfully = false;
			bool shouldUpdateControlFile = false;
			string? lockId = null;

			try
			{
				(lockAcquiredSuccessfully, lockId) = await _lockingService.AcquireNamedLock(ControlFileName);
				if (lockAcquiredSuccessfully)
				{
					_logger.Warning("Running recurring tasks on instance {name}.", Environment.MachineName);

                    await Task.WhenAll(_tasks.Select(t => t.ExecuteAsync(CancellationToken)));

					_logger.Warning("All recurring tasks executed successfully on instance {name}.", Environment.MachineName);
					shouldUpdateControlFile = true;
				}
				else
				{
					_logger.Warning("Will not execute recurring tasks on instance {name}. Another instance will handle this.", Environment.MachineName);
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
					await _lockingService.ReleaseNamedLock(ControlFileName, lockId!);
					if (shouldUpdateControlFile)
					{
						await _storageService.WriteAllTextToFile(ControlFileName, $"Completed at {_timeService.DateTimeUtcNow():u} on instance {Environment.MachineName}.");
					}
                }
			}
		}
	}
}
