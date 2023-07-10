using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using MySql.Data.MySqlClient;
using PhpbbInDotnet.Database.SqlExecuter;
using PhpbbInDotnet.Domain;
using System;
using System.Data;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddSqlExecuter(this IServiceCollection services)
        {
            services.AddTransient<IDbConnection>(sp =>
            {
                var configuration = sp.GetRequiredService<IConfiguration>();
                var databaseType = configuration.GetValue<DatabaseType>("Database:DatabaseType");
                var connStr = configuration.GetValue<string>("Database:ConnectionString");
                return databaseType switch
                {
                    DatabaseType.MySql => new MySqlConnection(connStr),
                    DatabaseType.SqlServer => new SqlConnection(connStr),
                    _ => throw new ArgumentException("Unknown Database type in configuration.")
                };
            });
			services.AddTransient<ISqlExecuter, SqlExecuter>();
            return services;
        }
    }
}
