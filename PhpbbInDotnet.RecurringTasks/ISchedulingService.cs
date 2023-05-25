using System;
using System.Threading.Tasks;

namespace PhpbbInDotnet.RecurringTasks
{
	public interface ISchedulingService
	{
		Task<TimeSpan> GetTimeToWaitUntilRunIsAllowed();
	}
}