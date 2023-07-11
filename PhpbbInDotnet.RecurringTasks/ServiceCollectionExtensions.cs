using Coravel;
using PhpbbInDotnet.RecurringTasks;
using PhpbbInDotnet.RecurringTasks.Tasks;

namespace Microsoft.Extensions.DependencyInjection
{
	public static class ServiceCollectionExtensions
	{
		public static IServiceCollection AddRecurringTasks(this IServiceCollection services)
		{
			services.AddScoped<IRecurringTask, ForumsAndTopicsSynchronizer>();
			services.AddScoped<IRecurringTask, LogCleaner>();
			services.AddScoped<IRecurringTask, OrphanFilesCleaner>();
			services.AddScoped<IRecurringTask, RecycleBinCleaner>();
			services.AddScoped<IRecurringTask, SiteMapGenerator>();

			services.AddScoped<Orchestrator>();

            services.AddScheduler();

            return services;
		}
	}
}
