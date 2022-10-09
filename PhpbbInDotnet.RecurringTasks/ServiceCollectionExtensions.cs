using Microsoft.Extensions.DependencyInjection;

namespace PhpbbInDotnet.RecurringTasks
{
	public static class ServiceCollectionExtensions
	{
		public static IServiceCollection AddRecurringTasks(this IServiceCollection services, params Type[] taskTypes)
		{
			services.AddSingleton<ISchedulingService, SchedulingService>();
			services.AddHostedService(sp => new Orchestrator(sp, sp.GetRequiredService<ISchedulingService>(), taskTypes));
			return services;
		}
	}
}
