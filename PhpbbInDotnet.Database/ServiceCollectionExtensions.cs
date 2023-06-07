using PhpbbInDotnet.Database.SqlExecuter;

namespace Microsoft.Extensions.DependencyInjection
{
	public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddSqlExecuter(this IServiceCollection services)
        {
			services.AddScoped<ISqlExecuter, SqlExecuter>();
            return services;
        }
    }
}
