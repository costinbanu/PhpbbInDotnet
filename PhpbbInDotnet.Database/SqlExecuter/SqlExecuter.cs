using Dapper;
using Microsoft.Extensions.Configuration;
using PhpbbInDotnet.Domain;
using Serilog;
using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;

namespace PhpbbInDotnet.Database.SqlExecuter
{
    sealed class SqlExecuter(IConfiguration configuration, ILogger logger) : DapperProxy(configuration, logger), ISqlExecuter
    {
        private readonly IConfiguration _configuration = configuration;
        private readonly ILogger _logger = logger;

        public IEnumerable<T> CallStoredProcedure<T>(string storedProcedureName, object? param)
            => ResilientExecute(() =>
            {
                using var connection = GetDbConnection();
                return connection.Query<T>(BuildStoreProcedureCall(storedProcedureName, param), param, commandTimeout: TIMEOUT);
            });

        public Task<IEnumerable<T>> CallStoredProcedureAsync<T>(string storedProcedureName, object? param)
            => ResilientExecuteAsync(() =>
            {
                using var connection = GetDbConnection();
                return connection.QueryAsync<T>(BuildStoreProcedureCall(storedProcedureName, param), param, commandTimeout: TIMEOUT);
            });

        public Task<SqlMapper.GridReader> CallMultipleResultsStoredProcedureAsync(string storedProcedureName, object? param)
            => QueryMultipleAsync(BuildStoreProcedureCall(storedProcedureName, param), param);

        public Task CallStoredProcedureAsync(string storedProcedureName, object? param)
            => ResilientExecuteAsync(() =>
            {
                using var connection = GetDbConnection();
                return connection.QueryAsync(BuildStoreProcedureCall(storedProcedureName, param), param, commandTimeout: TIMEOUT);
            });

        public IDapperProxy WithPagination(int skip, int take)
            => new PaginatedDapperProxy(skip, take, _configuration, _logger);

        public ITransactionalSqlExecuter BeginTransaction()
        {
            var defaultLevel = DatabaseType switch
            {
                DatabaseType.MySql => IsolationLevel.Serializable,
                DatabaseType.SqlServer => IsolationLevel.Snapshot,
                _ => throw new ArgumentException("Unknown Database type in configuration.")
            };

            return BeginTransaction(defaultLevel);
        }

        public ITransactionalSqlExecuter BeginTransaction(IsolationLevel isolationLevel)
        {
            if (isolationLevel == IsolationLevel.Snapshot && DatabaseType != DatabaseType.SqlServer)
            {
                throw new ArgumentException("The selected transaction isolation level is not compatible with the selected database type.", nameof(isolationLevel));
            }

            return new TransactionalSqlExecuter(isolationLevel, _configuration, _logger);
        }
    }
}
