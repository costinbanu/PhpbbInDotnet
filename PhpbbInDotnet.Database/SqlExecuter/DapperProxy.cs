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
		const int DURATION_INCREMENT = 5;
		const int MAX_RETRIES = 3;

		private readonly AsyncRetryPolicy _asyncRetryPolicy;

		protected readonly Lazy<IDbConnection> Connection;
		protected readonly DatabaseType DatabaseType;

		private readonly ILogger _logger;
		private readonly RetryPolicy _retryPolicy;

		public DapperProxy(IConfiguration configuration, ILogger logger)
		{
			_logger = logger;
			_asyncRetryPolicy = Policy.Handle<MySqlException>().Or<SqlException>().WaitAndRetryAsync(MAX_RETRIES, DurationProvider, OnRetry);
			_retryPolicy = Policy.Handle<MySqlException>().Or<SqlException>().WaitAndRetry(MAX_RETRIES, DurationProvider, OnRetry);

			DatabaseType = configuration.GetValue<DatabaseType>("Database:DatabaseType");
			var connStr = configuration.GetValue<string>("Database:ConnectionString");
			Connection = new Lazy<IDbConnection>(() =>
			{
				IDbConnection conn = DatabaseType switch
				{
					DatabaseType.MySql => new MySqlConnection(connStr),
					DatabaseType.SqlServer => new SqlConnection(connStr),
					_ => throw new ArgumentException("Unknown Database type in configuration.")
				};

				conn.Open();
				return conn;
			});
		}

		public void Dispose()
		{
			try
			{
				if (Connection.IsValueCreated)
				{
					Connection.Value.Dispose();
				}
			}
			catch { }
		}

		public Task<int> ExecuteAsync(string sql, object? param)
			=> ResilientExecuteAsync(() => Connection.Value.ExecuteAsync(sql, param));

		public T ExecuteScalar<T>(string sql, object? param)
			=> ResilientExecute(() => Connection.Value.ExecuteScalar<T>(sql, param));

		public Task<T> ExecuteScalarAsync<T>(string sql, object? param)
			=> ResilientExecuteAsync(() => Connection.Value.ExecuteScalarAsync<T>(sql, param));

		public IEnumerable<dynamic> Query(string sql, object? param)
			=> ResilientExecute(() => Connection.Value.Query(sql, param));

		public IEnumerable<T> Query<T>(string sql, object? param)
		{
			var result = _retryPolicy.ExecuteAndCapture(() => Connection.Value.Query<T>(sql, param));
			if (result.FinalException is not null)
			{
				throw result.FinalException;
			}
			return result.Result;
		}

		public Task<IEnumerable<dynamic>> QueryAsync(string sql, object? param)
			=> ResilientExecuteAsync(() => Connection.Value.QueryAsync(sql, param));

		public Task<IEnumerable<T>> QueryAsync<T>(string sql, object? param)
			=> ResilientExecuteAsync(() => Connection.Value.QueryAsync<T>(sql, param));

		public T QueryFirstOrDefault<T>(string sql, object? param)
			=> ResilientExecute(() => Connection.Value.QueryFirstOrDefault<T>(sql, param));

		public Task<dynamic> QueryFirstOrDefaultAsync(string sql, object? param)
			=> ResilientExecuteAsync(() => Connection.Value.QueryFirstOrDefaultAsync(sql, param));

		public Task<T> QueryFirstOrDefaultAsync<T>(string sql, object? param)
			=> ResilientExecuteAsync(() => Connection.Value.QueryFirstOrDefaultAsync<T>(sql, param));

		public T QuerySingle<T>(string sql, object? param)
			=> ResilientExecute(() => Connection.Value.QuerySingle<T>(sql, param));

		public Task<T> QuerySingleAsync<T>(string sql, object? param)
			=> ResilientExecuteAsync(() => Connection.Value.QuerySingleAsync<T>(sql, param));

		public Task<dynamic> QuerySingleOrDefaultAsync(string sql, object? param)
			=> ResilientExecuteAsync(() => Connection.Value.QuerySingleOrDefaultAsync(sql, param));

		public Task<T> QuerySingleOrDefaultAsync<T>(string sql, object? param)
			=> ResilientExecuteAsync(() => Connection.Value.QuerySingleOrDefaultAsync<T>(sql, param));

		private TimeSpan DurationProvider(int retryCount)
			=> TimeSpan.FromSeconds(retryCount * DURATION_INCREMENT);

		private void OnRetry(Exception ex, TimeSpan duration, int retryCount, Context context)
			=> _logger.Warning(ex, "An error occurred, will retry after {duration} for at most {maxRetries} times, current retry count: {count}.", duration, MAX_RETRIES, retryCount);

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
				throw new Exception("A SQL error occurred.", result.FinalException);
			}
			return result.Result;
		}
	}
}