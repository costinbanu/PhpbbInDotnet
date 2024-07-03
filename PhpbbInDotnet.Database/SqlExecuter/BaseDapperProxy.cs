using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using MySql.Data.MySqlClient;
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
    abstract class BaseDapperProxy
    {
        protected internal const int TIMEOUT = 60;
        static readonly TimeSpan[] DURATIONS =
        [
            TimeSpan.FromSeconds(1),
            TimeSpan.FromSeconds(1),
            TimeSpan.FromSeconds(2),
            TimeSpan.FromSeconds(3),
            TimeSpan.FromSeconds(5)
        ];
        protected const string PAGINATION_WILDCARD = "##paginate";

        private readonly AsyncRetryPolicy _asyncRetryPolicy;
        private readonly ILogger _logger;
        private readonly RetryPolicy _retryPolicy;
        private readonly string _connectionString;

        protected readonly DatabaseType DatabaseType;

        protected BaseDapperProxy(IConfiguration configuration, ILogger logger)
        {
            _logger = logger;
            _asyncRetryPolicy = Policy.Handle<Exception>(ExceptionFilter).WaitAndRetryAsync(DURATIONS, OnRetry);
            _retryPolicy = Policy.Handle<Exception>(ExceptionFilter).WaitAndRetry(DURATIONS, OnRetry);
            
            _connectionString = configuration.GetValue<string>("Database:ConnectionString")!;
            DatabaseType = configuration.GetValue<DatabaseType>("Database:DatabaseType");
        }

        public string LastInsertedItemId => DatabaseType switch
        {
            DatabaseType.MySql => "LAST_INSERT_ID()",
            DatabaseType.SqlServer => "SCOPE_IDENTITY()",
            _ => throw new ArgumentException("Unknown Database type in configuration.")
        };

        public string PaginationWildcard => PAGINATION_WILDCARD;

        protected string BuildStoreProcedureCall(string storedProcedureName, object? param)
        {
            var format = DatabaseType switch
            {
                DatabaseType.MySql => "CALL {0}({1})",
                DatabaseType.SqlServer => "EXEC {0} {1}",
                _ => throw new ArgumentException("Unknown Database type in configuration.")
            };
            var @params = param is not null ? param.GetType().GetProperties().Select(prop => $"@{prop.Name}") : Enumerable.Empty<string>();
            return string.Format(format, storedProcedureName, string.Join(",", @params));
        }

        protected T ResilientExecute<T>(Func<T> toDo)
            => ReturnResultOrThrowExceptionIfAny(_retryPolicy.ExecuteAndCapture(_ => toDo(), ContextData));

        protected void ResilientExecute(Action toDo)
            => _retryPolicy.Execute(toDo);

        protected async Task<T> ResilientExecuteAsync<T>(Func<Task<T>> toDo)
            => ReturnResultOrThrowExceptionIfAny(await _asyncRetryPolicy.ExecuteAndCaptureAsync(_ => toDo(), ContextData));

        protected IDbConnection GetDbConnection()
        {
            IDbConnection connection = DatabaseType switch
            {
                DatabaseType.MySql => new MySqlConnection(_connectionString),
                DatabaseType.SqlServer => new SqlConnection(_connectionString),
                _ => throw new ArgumentException("Unknown Database type in configuration.")
            };
            return connection;
        }

        private void OnRetry(Exception ex, TimeSpan duration, int retryCount, Context context)
        {
            var originalStackTrace = context.TryGetValue(nameof(Environment.StackTrace), out var stackTrace) ? stackTrace.ToString() : null;
            var myStackTrace = string.Join(Environment.NewLine, originalStackTrace?
                .Split(Environment.NewLine.ToCharArray(), StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(x => x.StartsWith("at PhpbbInDotnet"))
                .EmptyIfNull()!);

            _logger.Warning(
                new DatabaseException(ex.Message, myStackTrace),
                "An error occurred, will retry after {duration} for at most {maxRetries} times, current retry count: {count}, retry correlation id: {correlationId}.",
                duration, DURATIONS.Length, retryCount, context.CorrelationId);
        }

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

        private static bool ExceptionFilter(Exception ex)
            => !ex.Message.Contains("transaction aborted due to update conflict", StringComparison.InvariantCultureIgnoreCase);
    }
}
