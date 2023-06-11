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
using System.Data.Common;
using System.Threading.Tasks;

namespace PhpbbInDotnet.Database.SqlExecuter
{
	class DapperProxy : IDapperProxy, IDisposable
	{
		protected const int TIMEOUT = 60;
		const int DURATION_INCREMENT = 2;
		const int MAX_RETRIES = 3;

		private readonly AsyncRetryPolicy _asyncRetryPolicy;
		private readonly DbConnection _connection;
		private readonly ILogger _logger;
		private readonly RetryPolicy _retryPolicy;

		protected readonly DatabaseType DatabaseType;

		public DapperProxy(IConfiguration configuration, ILogger logger)
		{
			_logger = logger;
			_asyncRetryPolicy = Policy.Handle<MySqlException>().Or<SqlException>().WaitAndRetryAsync(MAX_RETRIES, DurationProvider, OnRetry);
			_retryPolicy = Policy.Handle<MySqlException>().Or<SqlException>().WaitAndRetry(MAX_RETRIES, DurationProvider, OnRetry);

			DatabaseType = configuration.GetValue<DatabaseType>("Database:DatabaseType");
			var connStr = configuration.GetValue<string>("Database:ConnectionString");
			_connection = DatabaseType switch
			{
				DatabaseType.MySql => new MySqlConnection(connStr),
				DatabaseType.SqlServer => new SqlConnection(connStr),
				_ => throw new ArgumentException("Unknown Database type in configuration.")
			};
		}

		public void Dispose()
		{
			try
			{
				_connection.Dispose();
			}
			catch { }
		}

		public Task<int> ExecuteAsync(string sql, object? param)
			=> ResilientExecuteAsync(async () => await (await GetDbConnectionAsync()).ExecuteAsync(sql, param, commandTimeout: TIMEOUT));

		public T ExecuteScalar<T>(string sql, object? param)
			=> ResilientExecute(() => GetDbConnection().ExecuteScalar<T>(sql, param, commandTimeout: TIMEOUT));

		public Task<T> ExecuteScalarAsync<T>(string sql, object? param)
			=> ResilientExecuteAsync(async () => await (await GetDbConnectionAsync()).ExecuteScalarAsync<T>(sql, param, commandTimeout: TIMEOUT));

		public IEnumerable<dynamic> Query(string sql, object? param)
			=> ResilientExecute(() => GetDbConnection().Query(sql, param, commandTimeout: TIMEOUT));

		public IEnumerable<T> Query<T>(string sql, object? param)
		{
			var result = _retryPolicy.ExecuteAndCapture(() => GetDbConnection().Query<T>(sql, param, commandTimeout: TIMEOUT));
			if (result.FinalException is not null)
			{
				throw result.FinalException;
			}
			return result.Result;
		}

		public Task<IEnumerable<dynamic>> QueryAsync(string sql, object? param)
			=> ResilientExecuteAsync(async () => await (await GetDbConnectionAsync()).QueryAsync(sql, param, commandTimeout: TIMEOUT));

		public Task<IEnumerable<T>> QueryAsync<T>(string sql, object? param)
			=> ResilientExecuteAsync(async () => await (await GetDbConnectionAsync()).QueryAsync<T>(sql, param, commandTimeout: TIMEOUT));

		public T QueryFirstOrDefault<T>(string sql, object? param)
			=> ResilientExecute(() => GetDbConnection().QueryFirstOrDefault<T>(sql, param, commandTimeout: TIMEOUT));

		public Task<dynamic> QueryFirstOrDefaultAsync(string sql, object? param)
			=> ResilientExecuteAsync(async () => await (await GetDbConnectionAsync()).QueryFirstOrDefaultAsync(sql, param, commandTimeout: TIMEOUT));

		public Task<T> QueryFirstOrDefaultAsync<T>(string sql, object? param)
			=> ResilientExecuteAsync(async () => await (await GetDbConnectionAsync()).QueryFirstOrDefaultAsync<T>(sql, param, commandTimeout: TIMEOUT));

		public T QuerySingle<T>(string sql, object? param)
			=> ResilientExecute(() => GetDbConnection().QuerySingle<T>(sql, param, commandTimeout: TIMEOUT));

		public Task<T> QuerySingleAsync<T>(string sql, object? param)
			=> ResilientExecuteAsync(async () => await (await GetDbConnectionAsync()).QuerySingleAsync<T>(sql, param, commandTimeout: TIMEOUT));

		public Task<dynamic> QuerySingleOrDefaultAsync(string sql, object? param)
			=> ResilientExecuteAsync(async () => await (await GetDbConnectionAsync()).QuerySingleOrDefaultAsync(sql, param, commandTimeout: TIMEOUT));

		public Task<T> QuerySingleOrDefaultAsync<T>(string sql, object? param)
			=> ResilientExecuteAsync(async () => await (await GetDbConnectionAsync()).QuerySingleOrDefaultAsync<T>(sql, param, commandTimeout: TIMEOUT));

		private TimeSpan DurationProvider(int retryCount)
			=> TimeSpan.FromSeconds(retryCount * DURATION_INCREMENT);

		private void OnRetry(Exception ex, TimeSpan duration, int retryCount, Context context)
			=> _logger.Warning(
					new Exception($"A SQL error occurred. Retry policy correlation id: {context.CorrelationId}.", ex), 
					"An error occurred, will retry after {duration} for at most {maxRetries} times, current retry count: {count}.", 
					duration, MAX_RETRIES, retryCount, context.CorrelationId);

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

		protected async Task<DbConnection> GetDbConnectionAsync()
		{
			if (_connection.State == ConnectionState.Closed)
			{
				await _connection.OpenAsync();
			}
			return _connection;
		}

		protected DbConnection GetDbConnection()
		{
			if (_connection.State == ConnectionState.Closed)
			{
				_connection.Open();
			}
			return _connection;
		}
	}
}