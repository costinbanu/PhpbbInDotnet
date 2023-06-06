using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using PhpbbInDotnet.Database.DbContexts;
using PhpbbInDotnet.Database.SqlExecuter;
using PhpbbInDotnet.Domain;
using System;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddForumDbContext(this IServiceCollection services, IConfiguration configuration)
        {
            var dbType = configuration.GetValue<DatabaseType>("Database:DatabaseType");
            var connStr = configuration.GetValue<string>("Database:ConnectionString");
            switch (dbType)
            {
                case DatabaseType.SqlServer:
                    services.AddDbContext<IForumDbContext, SqlServerDbContext>(options => options.UseSqlServer(connStr));
                    break;

                case DatabaseType.MySql:
					services.AddDbContext<IForumDbContext, MySqlDbContext>(options => options.UseMySQL(connStr));
                    break;

                default:
                    throw new ArgumentException("Unknown Database type in configuration.");
			}
			services.AddScoped<ISqlExecuter, SqlExecuter>();
            return services;
        }
    }
}
