using Dapper;
using Microsoft.EntityFrameworkCore;
using MySql.Data.MySqlClient;
using Polly;
using Polly.Retry;
using Serilog;
using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;

namespace PhpbbInDotnet.Database
{
	class SqlExecuter : ISqlExecuter
    {
		const int MAX_RETRIES = 3;
		const int DURATION_INCREMENT = 5;

		private readonly Lazy<IDbConnection> _connection;
		private readonly ILogger _logger;

		private readonly AsyncRetryPolicy _asyncRetryPolicy;
		private readonly RetryPolicy _retryPolicy;

        public SqlExecuter(IForumDbContext forumDbContext, ILogger logger)
        {
            _logger = logger;
            _asyncRetryPolicy = Policy.Handle<MySqlException>().WaitAndRetryAsync(MAX_RETRIES, DurationProvider, OnRetry);
			_retryPolicy = Policy.Handle<MySqlException>().WaitAndRetry(MAX_RETRIES, DurationProvider, OnRetry);

			_connection = new Lazy<IDbConnection>(() =>
            {
                var conn = forumDbContext.Database.GetDbConnection();
                if (conn.State == ConnectionState.Closed)
                {
                    conn.Open();
                }
                return conn;
            });
        }

        public async Task<int> ExecuteAsync(string sql, object? param)
        {
            var result = await _asyncRetryPolicy.ExecuteAndCaptureAsync(() => _connection.Value.ExecuteAsync(sql, param));
            if (result.FinalException is not null)
            {
                throw result.FinalException;
            }
            return result.Result;
        }

        public T ExecuteScalar<T>(string sql, object? param)
        {
            var result = _retryPolicy.ExecuteAndCapture(() => _connection.Value.ExecuteScalar<T>(sql, param));
            if (result.FinalException is not null)
            {
                throw result.FinalException;
            }
            return result.Result;
        }

		public async Task<T> ExecuteScalarAsync<T>(string sql, object? param)
        {
            var result = await _asyncRetryPolicy.ExecuteAndCaptureAsync(() => _connection.Value.ExecuteScalarAsync<T>(sql, param));
			if (result.FinalException is not null)
			{
				throw result.FinalException;
			}
			return result.Result;
        }

        public IEnumerable<T> Query<T>(string sql, object? param)
        {
            var result = _retryPolicy.ExecuteAndCapture(() => _connection.Value.Query<T>(sql, param));
            if (result.FinalException is not null)
            {
                throw result.FinalException;
            }
            return result.Result;
        }

        public IEnumerable<dynamic> Query(string sql, object? param)
        {
            var result = _retryPolicy.ExecuteAndCapture(() => _connection.Value.Query(sql, param));
            if (result.FinalException is not null)
            {
                throw result.FinalException;
            }
            return result.Result;
        }

		public async Task<IEnumerable<T>> QueryAsync<T>(string sql, object? param)
        {
            var result = await _asyncRetryPolicy.ExecuteAndCaptureAsync(() => _connection.Value.QueryAsync<T>(sql, param));
			if (result.FinalException is not null)
			{
				throw result.FinalException;
			}
			return result.Result;
        }

        public async Task<IEnumerable<dynamic>> QueryAsync(string sql, object? param)
        {
            var result = await _asyncRetryPolicy.ExecuteAndCaptureAsync(() => _connection.Value.QueryAsync(sql, param));
			if (result.FinalException is not null)
			{
				throw result.FinalException;
			}
			return result.Result;
        }

        public T QueryFirstOrDefault<T>(string sql, object? param)
        {
            var result = _retryPolicy.ExecuteAndCapture(() => _connection.Value.QueryFirstOrDefault<T>(sql, param));
            if (result.FinalException is not null)
            {
                throw result.FinalException;
            }
            return result.Result;
        }

		public async Task<T> QueryFirstOrDefaultAsync<T>(string sql, object? param)
        {
            var result = await _asyncRetryPolicy.ExecuteAndCaptureAsync(() => _connection.Value.QueryFirstOrDefaultAsync<T>(sql, param));
			if (result.FinalException is not null)
			{
				throw result.FinalException;
			}
			return result.Result;
        }

        public async Task<dynamic> QueryFirstOrDefaultAsync(string sql, object? param)
        {
            var result = await _asyncRetryPolicy.ExecuteAndCaptureAsync(() => _connection.Value.QueryFirstOrDefaultAsync(sql, param));
			if (result.FinalException is not null)
			{
				throw result.FinalException;
			}
			return result.Result;
        }

        public async Task<T> QuerySingleOrDefaultAsync<T>(string sql, object? param)
        {
            var result = await _asyncRetryPolicy.ExecuteAndCaptureAsync(() => _connection.Value.QuerySingleOrDefaultAsync<T>(sql, param));
			if (result.FinalException is not null)
			{
				throw result.FinalException;
			}
			return result.Result;
        }

        public T QuerySingle<T>(string sql, object? param)
        {
            var result = _retryPolicy.ExecuteAndCapture(() => _connection.Value.QuerySingle<T>(sql, param));
            if (result.FinalException is not null)
            {
                throw result.FinalException;
            }
            return result.Result;
        }

	    public async Task<T> QuerySingleAsync<T>(string sql, object? param)
        {
            var result = await _asyncRetryPolicy.ExecuteAndCaptureAsync(() => _connection.Value.QuerySingleAsync<T>(sql, param));
			if (result.FinalException is not null)
			{
				throw result.FinalException;
			}
			return result.Result;
        }

        public async Task<dynamic> QuerySingleOrDefaultAsync(string sql, object? param)
        {
            var result = await _asyncRetryPolicy.ExecuteAndCaptureAsync(() => _connection.Value.QuerySingleOrDefaultAsync(sql, param));
			if (result.FinalException is not null)
			{
				throw result.FinalException;
			}
			return result.Result;
        }

		private TimeSpan DurationProvider(int retryCount)
	        => TimeSpan.FromSeconds(retryCount * DURATION_INCREMENT);

		private void OnRetry(Exception ex, TimeSpan duration, int retryCount, Context context)
			=> _logger.Warning(ex, "An error occurred, will retry after {duration} for at most {maxRetries} times, current retry count: {count}.", duration, MAX_RETRIES, retryCount);
	}
}
