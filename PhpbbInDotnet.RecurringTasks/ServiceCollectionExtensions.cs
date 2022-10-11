using PhpbbInDotnet.RecurringTasks;
using PhpbbInDotnet.RecurringTasks.Tasks;

namespace Microsoft.Extensions.DependencyInjection
{
	public static class ServiceCollectionExtensions
	{
		public static IServiceCollection AddRecurringTasks(this IServiceCollection services)
		{
			services.AddSingleton<ISchedulingService, SchedulingService>();
			services.AddHostedService(serviceProvider => new Orchestrator(
				serviceProvider, 
				typeof(ForumsAndTopicsSynchronizer), 
				typeof(LogCleaner), 
				typeof(OrphanFilesCleaner), 
				typeof(RecycleBinCleaner), 
				typeof(SiteMapGenerator)));
			return services;
		}
	}
}
