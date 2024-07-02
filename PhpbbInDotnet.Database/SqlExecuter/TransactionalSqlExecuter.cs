using Dapper;
using Microsoft.Extensions.Configuration;
using Serilog;
using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;

namespace PhpbbInDotnet.Database.SqlExecuter
{
    sealed class TransactionalSqlExecuter : BaseDapperProxy, ITransactionalSqlExecuter
    {
        private readonly IDbConnection _connection;
        private readonly IDbTransaction _transaction;

        internal TransactionalSqlExecuter(IsolationLevel isolationLevel, IConfiguration configuration, ILogger logger) : base(configuration, logger)
        {
            _connection = GetDbConnection();
            _transaction = _connection.BeginTransaction(isolationLevel);
        }

        public ITransactionalSqlExecuter BeginTransaction(IsolationLevel _) 
            => throw new NotSupportedException("Transaction already started");

        public ITransactionalSqlExecuter BeginTransaction() 
            => throw new NotSupportedException("Transaction already started");

        public IDapperProxy WithPagination(int skip, int take) 
            => throw new NotSupportedException("Paginated queries within a transaction are not supported");

        public IEnumerable<T> CallStoredProcedure<T>(string storedProcedureName, object? param)
            => ResilientExecute(() => _connection.Query<T>(BuildStoreProcedureCall(storedProcedureName, param), param, _transaction, commandTimeout: TIMEOUT));

        public Task<IEnumerable<T>> CallStoredProcedureAsync<T>(string storedProcedureName, object? param)
            => ResilientExecuteAsync(() => _connection.QueryAsync<T>(BuildStoreProcedureCall(storedProcedureName, param), param, _transaction, commandTimeout: TIMEOUT));

        public Task<SqlMapper.GridReader> CallMultipleResultsStoredProcedureAsync(string storedProcedureName, object? param)
            => ResilientExecuteAsync(() => _connection.QueryMultipleAsync(BuildStoreProcedureCall(storedProcedureName, param), param, _transaction, commandTimeout: TIMEOUT)); 

        public Task CallStoredProcedureAsync(string storedProcedureName, object? param)
            => ResilientExecuteAsync(() => _connection.QueryAsync(BuildStoreProcedureCall(storedProcedureName, param), param, _transaction, commandTimeout: TIMEOUT));

        public Task<int> ExecuteAsyncWithoutResiliency(string sql, object? param = null, int commandTimeout = TIMEOUT) 
            => _connection.ExecuteAsync(sql, param, _transaction, commandTimeout: commandTimeout);

        public Task<int> ExecuteAsync(string sql, object? param) 
            => ResilientExecuteAsync(() => _connection.ExecuteAsync(sql, param, _transaction, commandTimeout: TIMEOUT));

        public T? ExecuteScalar<T>(string sql, object? param) 
            => ResilientExecute(() => _connection.ExecuteScalar<T>(sql, param, _transaction, commandTimeout: TIMEOUT));

        public Task<T?> ExecuteScalarAsync<T>(string sql, object? param) 
            => ResilientExecuteAsync(() => _connection.ExecuteScalarAsync<T>(sql, param, _transaction, commandTimeout: TIMEOUT));

        public IEnumerable<dynamic> Query(string sql, object? param) 
            => ResilientExecute(() => _connection.Query(sql, param, _transaction, commandTimeout: TIMEOUT));

        public IEnumerable<T> Query<T>(string sql, object? param) 
            => ResilientExecute(() => _connection.Query<T>(sql, param, _transaction, commandTimeout: TIMEOUT));

        public Task<IEnumerable<dynamic>> QueryAsync(string sql, object? param) 
            => ResilientExecuteAsync(() => _connection.QueryAsync(sql, param, _transaction, commandTimeout: TIMEOUT));

        public Task<IEnumerable<T>> QueryAsync<T>(string sql, object? param) 
            => ResilientExecuteAsync(() => _connection.QueryAsync<T>(sql, param, _transaction, commandTimeout: TIMEOUT));

        public T? QueryFirstOrDefault<T>(string sql, object? param) 
            => ResilientExecute(() => _connection.QueryFirstOrDefault<T>(sql, param, _transaction, commandTimeout: TIMEOUT));

        public Task<dynamic?> QueryFirstOrDefaultAsync(string sql, object? param) 
            => ResilientExecuteAsync(() => _connection.QueryFirstOrDefaultAsync(sql, param, _transaction, commandTimeout: TIMEOUT));

        public Task<T?> QueryFirstOrDefaultAsync<T>(string sql, object? param) 
            => ResilientExecuteAsync(() => _connection.QueryFirstOrDefaultAsync<T>(sql, param, _transaction, commandTimeout: TIMEOUT));

        public T QuerySingle<T>(string sql, object? param) 
            => ResilientExecute(() => _connection.QuerySingle<T>(sql, param, _transaction, commandTimeout: TIMEOUT));

        public Task<T> QuerySingleAsync<T>(string sql, object? param) 
            => ResilientExecuteAsync(() => _connection.QuerySingleAsync<T>(sql, param, _transaction, commandTimeout: TIMEOUT));

        public Task<dynamic?> QuerySingleOrDefaultAsync(string sql, object? param) 
            => ResilientExecuteAsync(() => _connection.QuerySingleOrDefaultAsync(sql, param, _transaction, commandTimeout: TIMEOUT));

        public Task<T?> QuerySingleOrDefaultAsync<T>(string sql, object? param) 
            => ResilientExecuteAsync(() => _connection.QuerySingleOrDefaultAsync<T>(sql, param, _transaction, commandTimeout: TIMEOUT));

        public Task<SqlMapper.GridReader> QueryMultipleAsync(string sql, object? param) 
            => ResilientExecuteAsync(() => _connection.QueryMultipleAsync(sql, param, _transaction, commandTimeout: TIMEOUT));

        public void CommitTransaction()
            => _transaction.Commit();

        public void Dispose()
        {
            try
            { 
                _connection.Dispose(); 
            } catch { }

            try
            { 
                _transaction.Dispose(); 
            }
            catch { }
        }
    }
}
