using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using MySql.Data.MySqlClient;
using PhpbbInDotnet.Domain;
using Polly;
using Polly.Retry;
using Serilog;
using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;

namespace PhpbbInDotnet.Database.SqlExecuter
{
	class DapperProxy : IDapperProxy, IDisposable
	{
		protected const int TIMEOUT = 60;
		const int DURATION_INCREMENT = 2;
		const int MAX_RETRIES = 3;

		private readonly AsyncRetryPolicy _asyncRetryPolicy;
		private readonly ILogger _logger;
		private readonly RetryPolicy _retryPolicy;

		protected readonly DatabaseType DatabaseType;
		protected readonly IDbConnection Connection;

		public DapperProxy(IConfiguration configuration, ILogger logger)
		{
			_logger = logger;
			_asyncRetryPolicy = Policy.Handle<Exception>().WaitAndRetryAsync(MAX_RETRIES, DurationProvider, OnRetry);
			_retryPolicy = Policy.Handle<Exception>().WaitAndRetry(MAX_RETRIES, DurationProvider, OnRetry);

			DatabaseType = configuration.GetValue<DatabaseType>("Database:DatabaseType");
			var connStr = configuration.GetValue<string>("Database:ConnectionString");
			Connection = DatabaseType switch
			{
				DatabaseType.MySql => new MySqlConnection(connStr),
				DatabaseType.SqlServer => new SqlConnection(connStr),
				_ => throw new ArgumentException("Unknown Database type in configuration.")
			};
			ResilientExecute(() => Connection.Open());
		}

		public void Dispose()
		{
			try
			{
				Connection.Dispose();
			}
			catch { }
		}

		public Task<int> ExecuteAsync(string sql, object? param)
			=> ResilientExecuteAsync(() => Connection.ExecuteAsync(sql, param, commandTimeout: TIMEOUT));

		public T ExecuteScalar<T>(string sql, object? param)
			=> ResilientExecute(() => Connection.ExecuteScalar<T>(sql, param, commandTimeout: TIMEOUT));

		public Task<T> ExecuteScalarAsync<T>(string sql, object? param)
			=> ResilientExecuteAsync(() => Connection.ExecuteScalarAsync<T>(sql, param, commandTimeout: TIMEOUT));

		public IEnumerable<dynamic> Query(string sql, object? param)
			=> ResilientExecute(() => Connection.Query(sql, param, commandTimeout: TIMEOUT));

		public IEnumerable<T> Query<T>(string sql, object? param)
			=> ResilientExecute(() => Connection.Query<T>(sql, param, commandTimeout: TIMEOUT));

		public Task<IEnumerable<dynamic>> QueryAsync(string sql, object? param)
			=> ResilientExecuteAsync(() => Connection.QueryAsync(sql, param, commandTimeout: TIMEOUT));

		public Task<IEnumerable<T>> QueryAsync<T>(string sql, object? param)
			=> ResilientExecuteAsync(() => Connection.QueryAsync<T>(sql, param, commandTimeout: TIMEOUT));

		public T QueryFirstOrDefault<T>(string sql, object? param)
			=> ResilientExecute(() => Connection.QueryFirstOrDefault<T>(sql, param, commandTimeout: TIMEOUT));

		public Task<dynamic> QueryFirstOrDefaultAsync(string sql, object? param)
			=> ResilientExecuteAsync(() => Connection.QueryFirstOrDefaultAsync(sql, param, commandTimeout: TIMEOUT));

		public Task<T> QueryFirstOrDefaultAsync<T>(string sql, object? param)
			=> ResilientExecuteAsync(() => Connection.QueryFirstOrDefaultAsync<T>(sql, param, commandTimeout: TIMEOUT));

		public T QuerySingle<T>(string sql, object? param)
			=> ResilientExecute(() => Connection.QuerySingle<T>(sql, param, commandTimeout: TIMEOUT));

		public Task<T> QuerySingleAsync<T>(string sql, object? param)
			=> ResilientExecuteAsync(() => Connection.QuerySingleAsync<T>(sql, param, commandTimeout: TIMEOUT));

		public Task<dynamic> QuerySingleOrDefaultAsync(string sql, object? param)
			=> ResilientExecuteAsync(() => Connection.QuerySingleOrDefaultAsync(sql, param, commandTimeout: TIMEOUT));

		public Task<T> QuerySingleOrDefaultAsync<T>(string sql, object? param)
			=> ResilientExecuteAsync(() => Connection.QuerySingleOrDefaultAsync<T>(sql, param, commandTimeout: TIMEOUT));

		private TimeSpan DurationProvider(int retryCount)
			=> TimeSpan.FromSeconds(/*retryCount **/ DURATION_INCREMENT);

		private void OnRetry(Exception ex, TimeSpan duration, int retryCount, Context context)
			=> _logger.Warning(
					new Exception($"A SQL error occurred. Retry policy correlation id: {context.CorrelationId}.", ex), 
					"An error occurred, will retry after {duration} for at most {maxRetries} times, current retry count: {count}.", 
					duration, MAX_RETRIES, retryCount, context.CorrelationId);

		protected void ResilientExecute(Action toDo)
		{
			var result = _retryPolicy.ExecuteAndCapture(toDo);
			if (result.FinalException is not null)
			{
				throw result.FinalException;
			}
		}

		protected T ResilientExecute<T>(Func<T> toDo)
		{
			var result = _retryPolicy.ExecuteAndCapture(toDo);
			if (result.FinalException is not null)
			{
				throw result.FinalException;
			}
			return result.Result;
		}

		protected async Task<T> ResilientExecuteAsync<T>(Func<Task<T>> toDo)
		{
			var result = await _asyncRetryPolicy.ExecuteAndCaptureAsync(toDo);
			if (result.FinalException is not null)
			{
				throw new Exception($"A SQL error occurred. Retry policy correlation id: {result.Context.CorrelationId}.", result.FinalException);
			}
			return result.Result;
		}
	}
}