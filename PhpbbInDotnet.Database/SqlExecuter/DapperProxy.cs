using Dapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using PhpbbInDotnet.Domain;
using PhpbbInDotnet.Domain.Extensions;
using Polly;
using Polly.Retry;
using Serilog;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;

namespace PhpbbInDotnet.Database.SqlExecuter
{
    class DapperProxy : IDapperProxy
	{
		protected const int TIMEOUT = 60;
		static readonly TimeSpan[] DURATIONS = new[]
		{
			TimeSpan.FromSeconds(1),
			TimeSpan.FromSeconds(1),
			TimeSpan.FromSeconds(2),
			TimeSpan.FromSeconds(3),
			TimeSpan.FromSeconds(5)
		};

		private readonly AsyncRetryPolicy _asyncRetryPolicy;
		private readonly ILogger _logger;
		private readonly RetryPolicy _retryPolicy;

		protected internal readonly DatabaseType DatabaseType;
		protected readonly IDbConnection Connection;

		public DapperProxy(IConfiguration configuration, IDbConnection dbConnection, ILogger logger)
		{
			_logger = logger;
			_asyncRetryPolicy = Policy.Handle<Exception>().WaitAndRetryAsync(DURATIONS, OnRetry);
			_retryPolicy = Policy.Handle<Exception>().WaitAndRetry(DURATIONS, OnRetry);

			DatabaseType = configuration.GetValue<DatabaseType>("Database:DatabaseType");
			Connection = dbConnection;
		}

        public Task<int> ExecuteAsync(string sql, object? param)
            => ExecuteAsyncImpl(sql, param, dbTransaction: null);

        public T ExecuteScalar<T>(string sql, object? param)
            => ExecuteScalarImpl<T>(sql, param, dbTransaction: null);

        public Task<T> ExecuteScalarAsync<T>(string sql, object? param)
            => ExecuteScalarAsyncImpl<T>(sql, param, dbTransaction: null);

        public IEnumerable<dynamic> Query(string sql, object? param)
            => QueryImpl(sql, param, dbTransaction: null);

        public IEnumerable<T> Query<T>(string sql, object? param)
            => QueryImpl<T>(sql, param, dbTransaction: null);

        public Task<IEnumerable<dynamic>> QueryAsync(string sql, object? param)
            => QueryAsyncImpl(sql, param, dbTransaction: null);

        public Task<IEnumerable<T>> QueryAsync<T>(string sql, object? param)
            => QueryAsyncImpl<T>(sql, param, dbTransaction: null);

        public T QueryFirstOrDefault<T>(string sql, object? param)
            => QueryFirstOrDefaultImpl<T>(sql, param, dbTransaction: null);

        public Task<dynamic> QueryFirstOrDefaultAsync(string sql, object? param)
            => QueryFirstOrDefaultAsyncImpl(sql, param, dbTransaction: null);

        public Task<T> QueryFirstOrDefaultAsync<T>(string sql, object? param)
            => QueryFirstOrDefaultAsyncImpl<T>(sql, param, dbTransaction: null);

        public T QuerySingle<T>(string sql, object? param)
            => QuerySingleImpl<T>(sql, param, dbTransaction: null);

        public Task<T> QuerySingleAsync<T>(string sql, object? param)
            => QuerySingleAsyncImpl<T>(sql, param, dbTransaction: null);

        public Task<dynamic> QuerySingleOrDefaultAsync(string sql, object? param)
            => QuerySingleOrDefaultAsyncImpl(sql, param, dbTransaction: null);

        public Task<T> QuerySingleOrDefaultAsync<T>(string sql, object? param)
            => QuerySingleOrDefaultAsyncImpl<T>(sql, param, dbTransaction: null);

		public Task<SqlMapper.GridReader> QueryMultipleAsync(string sql, object? param)
			=> QueryMultipleAsyncImpl(sql, param, dbTransaction: null);

        #region internal impl

        internal Task<int> ExecuteAsyncImpl(string sql, object? param, IDbTransaction? dbTransaction)
            => ResilientExecuteAsync(() => Connection.ExecuteAsync(sql, param, transaction: dbTransaction, commandTimeout: TIMEOUT));

        internal T ExecuteScalarImpl<T>(string sql, object? param, IDbTransaction? dbTransaction)
            => ResilientExecute(() => Connection.ExecuteScalar<T>(sql, param, transaction: dbTransaction, commandTimeout: TIMEOUT));

        internal Task<T> ExecuteScalarAsyncImpl<T>(string sql, object? param, IDbTransaction? dbTransaction)
            => ResilientExecuteAsync(() => Connection.ExecuteScalarAsync<T>(sql, param, transaction: dbTransaction, commandTimeout: TIMEOUT));

        internal IEnumerable<dynamic> QueryImpl(string sql, object? param, IDbTransaction? dbTransaction)
            => ResilientExecute(() => Connection.Query(sql, param, transaction: dbTransaction, commandTimeout: TIMEOUT));

        internal IEnumerable<T> QueryImpl<T>(string sql, object? param, IDbTransaction? dbTransaction)
            => ResilientExecute(() => Connection.Query<T>(sql, param, transaction: dbTransaction, commandTimeout: TIMEOUT));

        internal Task<IEnumerable<dynamic>> QueryAsyncImpl(string sql, object? param, IDbTransaction? dbTransaction)
            => ResilientExecuteAsync(() => Connection.QueryAsync(sql, param, transaction: dbTransaction, commandTimeout: TIMEOUT));

        internal Task<IEnumerable<T>> QueryAsyncImpl<T>(string sql, object? param, IDbTransaction? dbTransaction)
            => ResilientExecuteAsync(() => Connection.QueryAsync<T>(sql, param, transaction: dbTransaction, commandTimeout: TIMEOUT));

        internal T QueryFirstOrDefaultImpl<T>(string sql, object? param, IDbTransaction? dbTransaction)
            => ResilientExecute(() => Connection.QueryFirstOrDefault<T>(sql, param, transaction: dbTransaction, commandTimeout: TIMEOUT));

        internal Task<dynamic> QueryFirstOrDefaultAsyncImpl(string sql, object? param, IDbTransaction? dbTransaction)
            => ResilientExecuteAsync(() => Connection.QueryFirstOrDefaultAsync(sql, param, transaction: dbTransaction, commandTimeout: TIMEOUT));

        internal Task<T> QueryFirstOrDefaultAsyncImpl<T>(string sql, object? param, IDbTransaction? dbTransaction)
            => ResilientExecuteAsync(() => Connection.QueryFirstOrDefaultAsync<T>(sql, param, transaction: dbTransaction, commandTimeout: TIMEOUT));

        internal T QuerySingleImpl<T>(string sql, object? param, IDbTransaction? dbTransaction)
            => ResilientExecute(() => Connection.QuerySingle<T>(sql, param, transaction: dbTransaction, commandTimeout: TIMEOUT));

        internal Task<T> QuerySingleAsyncImpl<T>(string sql, object? param, IDbTransaction? dbTransaction)
            => ResilientExecuteAsync(() => Connection.QuerySingleAsync<T>(sql, param, transaction: dbTransaction, commandTimeout: TIMEOUT));

        internal Task<dynamic> QuerySingleOrDefaultAsyncImpl(string sql, object? param, IDbTransaction? dbTransaction)
            => ResilientExecuteAsync(() => Connection.QuerySingleOrDefaultAsync(sql, param, transaction: dbTransaction, commandTimeout: TIMEOUT));

        internal Task<T> QuerySingleOrDefaultAsyncImpl<T>(string sql, object? param, IDbTransaction? dbTransaction)
            => ResilientExecuteAsync(() => Connection.QuerySingleOrDefaultAsync<T>(sql, param, transaction: dbTransaction, commandTimeout: TIMEOUT));

        internal Task<SqlMapper.GridReader> QueryMultipleAsyncImpl(string sql, object? param, IDbTransaction? dbTransaction)
            => ResilientExecuteAsync(() => Connection.QueryMultipleAsync(sql, param, transaction: dbTransaction, commandTimeout: TIMEOUT));

        #endregion

        private void OnRetry(Exception ex, TimeSpan duration, int retryCount, Context context)
		{ 
			var originalStackTrace = context.TryGetValue(nameof(Environment.StackTrace), out var stackTrace) ? stackTrace.ToString() : null;
			var myStackTrace = string.Join(Environment.NewLine, originalStackTrace?
				.Split(Environment.NewLine.ToCharArray(), StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
				.Where(x => x.StartsWith("at PhpbbInDotnet"))
				.EmptyIfNull()!);

			_logger.Warning(
				new DatabaseException(ex.Message, myStackTrace),
				"An error occurred, will retry after {duration} for at most {maxRetries} times, current retry count: {count}, current connection state: {state}, retry correlation id: {correlationId}.",
				duration, DURATIONS.Length, retryCount, Connection.State, context.CorrelationId);
		}
		protected T ResilientExecute<T>(Func<T> toDo)
			=> ReturnResultOrThrowExceptionIfAny(_retryPolicy.ExecuteAndCapture(_ => toDo(), ContextData));

		protected async Task<T> ResilientExecuteAsync<T>(Func<Task<T>> toDo)
			=> ReturnResultOrThrowExceptionIfAny(await _asyncRetryPolicy.ExecuteAndCaptureAsync(_ => toDo(), ContextData));

		private static T ReturnResultOrThrowExceptionIfAny<T>(PolicyResult<T> result)
		{
            if (result.FinalException is not null)
            {
                throw new DatabaseException($"A SQL error occurred. Retry policy correlation id: {result.Context.CorrelationId}.", result.FinalException);
            }
            return result.Result;
        }

		private static IDictionary<string, object> ContextData
			=> new Dictionary<string, object>
			{
				[nameof(Environment.StackTrace)] = Environment.StackTrace
			};
	}
}