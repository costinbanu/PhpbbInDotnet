using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using PhpbbInDotnet.Database;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddForumDbContext(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddDbContext<IForumDbContext, ForumDbContext>(options => options.UseMySQL(configuration.GetValue<string>("ForumDbConnectionString")));
            services.AddScoped<ISqlExecuter, SqlExecuter>();
            return services;
        }
    }
}
