using System.Threading;
using System.Threading.Tasks;

namespace PhpbbInDotnet.RecurringTasks.Tasks
{
    interface IRecurringTask
    {
        Task ExecuteAsync(CancellationToken stoppingToken);
    }
}
