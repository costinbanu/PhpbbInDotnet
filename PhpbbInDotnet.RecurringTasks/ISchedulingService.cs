using System;

namespace PhpbbInDotnet.RecurringTasks
{
	public interface ISchedulingService
	{
		TimeSpan GetTimeToWaitUntilRunIsAllowed();
	}
}