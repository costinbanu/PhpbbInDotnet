namespace PhpbbInDotnet.RecurringTasks
{
	abstract class BaseRecurringTask
	{
		public abstract Task ExecuteAsync(CancellationToken stoppingToken);
	}
}