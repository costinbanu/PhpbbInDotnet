using Dapper;
using Microsoft.EntityFrameworkCore;
using MySql.Data.MySqlClient;
using Polly;
using Serilog;
using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;

namespace PhpbbInDotnet.Database
{
	class SqlExecuter : ISqlExecuter
    {
        private readonly Lazy<IDbConnection> _connection;

        public SqlExecuter(IForumDbContext forumDbContext, ILogger logger)
        {
            const int maxRetries = 3;
            const int durationIncrement = 5;
            var retryPolicy = Policy
                .Handle<MySqlException>()
                .WaitAndRetry(
                    retryCount: maxRetries,
                    sleepDurationProvider: count => TimeSpan.FromSeconds(count * durationIncrement),
                    onRetry: (ex, duration, count, _) => logger.Warning(ex, "An error occurred, will retry after {duration} for at most {maxRetries} times, current retry count: {count}.", duration, maxRetries, count));

			_connection = new Lazy<IDbConnection>(() =>
            {
                var conn = forumDbContext.Database.GetDbConnection();
                retryPolicy.Execute(() =>
                {
                    if (conn.State == ConnectionState.Closed)
                    {
                        conn.Open();
                    }
                });
                return conn;
            });
        }

        public Task<int> ExecuteAsync(string sql, object? param)
            => _connection.Value.ExecuteAsync(sql, param);

        public T ExecuteScalar<T>(string sql, object? param)
            => _connection.Value.ExecuteScalar<T>(sql, param);

        public Task<T> ExecuteScalarAsync<T>(string sql, object? param)
            => _connection.Value.ExecuteScalarAsync<T>(sql, param);

        public IEnumerable<T> Query<T>(string sql, object? param)
            => _connection.Value.Query<T>(sql, param);

        public IEnumerable<dynamic> Query(string sql, object? param)
            => _connection.Value.Query(sql, param);

        public Task<IEnumerable<T>> QueryAsync<T>(string sql, object? param)
            => _connection.Value.QueryAsync<T>(sql, param);

        public Task<IEnumerable<dynamic>> QueryAsync(string sql, object? param)
            => _connection.Value.QueryAsync(sql, param);

        public T QueryFirstOrDefault<T>(string sql, object? param)
            => _connection.Value.QueryFirstOrDefault<T>(sql, param);

        public Task<T> QueryFirstOrDefaultAsync<T>(string sql, object? param)
            => _connection.Value.QueryFirstOrDefaultAsync<T>(sql, param);

        public Task<dynamic> QueryFirstOrDefaultAsync(string sql, object? param)
            => _connection.Value.QueryFirstOrDefaultAsync(sql, param);

        public Task<T> QuerySingleOrDefaultAsync<T>(string sql, object? param)
            => _connection.Value.QuerySingleOrDefaultAsync<T>(sql, param);

        public T QuerySingle<T>(string sql, object? param)
            => _connection.Value.QuerySingle<T>(sql, param);

        public Task<T> QuerySingleAsync<T>(string sql, object? param)
            => _connection.Value.QuerySingleAsync<T>(sql, param);

        public Task<dynamic> QuerySingleOrDefaultAsync(string sql, object? param)
            => _connection.Value.QuerySingleOrDefaultAsync(sql, param);
    }
}
