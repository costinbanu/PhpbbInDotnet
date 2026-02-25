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
        private readonly bool _useResiliency;

        internal TransactionalSqlExecuter(IsolationLevel isolationLevel, bool useResiliency, IConfiguration configuration, ILogger logger) : base(configuration, logger)
        {
            _connection = GetDbConnection();
            _useResiliency = useResiliency;
            Execute(() => _connection.Open());
            _transaction = Execute(() => _connection.BeginTransaction(isolationLevel));
        }

        public ITransactionalSqlExecuter BeginTransaction(IsolationLevel _, bool __)
            => throw new NotSupportedException("Transaction already started");

        public ITransactionalSqlExecuter BeginTransaction(bool _)
            => throw new NotSupportedException("Transaction already started");

        public IDapperProxy WithPagination(int skip, int take)
            => throw new NotSupportedException("Paginated queries within a transaction are not supported");

        public IEnumerable<T> CallStoredProcedure<T>(string storedProcedureName, object? param)
            => Execute(() => _connection.Query<T>(BuildStoreProcedureCall(storedProcedureName, param), param, _transaction, commandTimeout: TIMEOUT));

        public Task<IEnumerable<T>> CallStoredProcedureAsync<T>(string storedProcedureName, object? param)
            => ExecuteAsync(() => _connection.QueryAsync<T>(BuildStoreProcedureCall(storedProcedureName, param), param, _transaction, commandTimeout: TIMEOUT));

        public Task<IMultipleResultsProxy> CallMultipleResultsStoredProcedureAsync(string storedProcedureName, object? param)
            => throw new NotSupportedException("Multiple result queries within a transaction are not supported");

        public Task CallStoredProcedureAsync(string storedProcedureName, object? param)
            => ExecuteAsync(() => _connection.QueryAsync(BuildStoreProcedureCall(storedProcedureName, param), param, _transaction, commandTimeout: TIMEOUT));

        public Task<int> ExecuteAsyncWithoutResiliency(string sql, object? param = null, int commandTimeout = TIMEOUT)
            => _connection.ExecuteAsync(sql, param, _transaction, commandTimeout: commandTimeout);

        public Task<int> ExecuteAsync(string sql, object? param)
            => ExecuteAsync(() => _connection.ExecuteAsync(sql, param, _transaction, commandTimeout: TIMEOUT));

        public T? ExecuteScalar<T>(string sql, object? param)
            => Execute(() => _connection.ExecuteScalar<T>(sql, param, _transaction, commandTimeout: TIMEOUT));

        public Task<T?> ExecuteScalarAsync<T>(string sql, object? param)
            => ExecuteAsync(() => _connection.ExecuteScalarAsync<T>(sql, param, _transaction, commandTimeout: TIMEOUT));

        public IEnumerable<dynamic> Query(string sql, object? param)
            => Execute(() => _connection.Query(sql, param, _transaction, commandTimeout: TIMEOUT));

        public IEnumerable<T> Query<T>(string sql, object? param)
            => Execute(() => _connection.Query<T>(sql, param, _transaction, commandTimeout: TIMEOUT));

        public Task<IEnumerable<dynamic>> QueryAsync(string sql, object? param)
            => ExecuteAsync(() => _connection.QueryAsync(sql, param, _transaction, commandTimeout: TIMEOUT));

        public Task<IEnumerable<T>> QueryAsync<T>(string sql, object? param)
            => ExecuteAsync(() => _connection.QueryAsync<T>(sql, param, _transaction, commandTimeout: TIMEOUT));

        public T? QueryFirstOrDefault<T>(string sql, object? param)
            => Execute(() => _connection.QueryFirstOrDefault<T>(sql, param, _transaction, commandTimeout: TIMEOUT));

        public Task<dynamic?> QueryFirstOrDefaultAsync(string sql, object? param)
            => ExecuteAsync(() => _connection.QueryFirstOrDefaultAsync(sql, param, _transaction, commandTimeout: TIMEOUT));

        public Task<T?> QueryFirstOrDefaultAsync<T>(string sql, object? param)
            => ExecuteAsync(() => _connection.QueryFirstOrDefaultAsync<T>(sql, param, _transaction, commandTimeout: TIMEOUT));

        public T QuerySingle<T>(string sql, object? param)
            => Execute(() => _connection.QuerySingle<T>(sql, param, _transaction, commandTimeout: TIMEOUT));

        public Task<T> QuerySingleAsync<T>(string sql, object? param)
            => ExecuteAsync(() => _connection.QuerySingleAsync<T>(sql, param, _transaction, commandTimeout: TIMEOUT));

        public Task<dynamic?> QuerySingleOrDefaultAsync(string sql, object? param)
            => ExecuteAsync(() => _connection.QuerySingleOrDefaultAsync(sql, param, _transaction, commandTimeout: TIMEOUT));

        public Task<T?> QuerySingleOrDefaultAsync<T>(string sql, object? param)
            => ExecuteAsync(() => _connection.QuerySingleOrDefaultAsync<T>(sql, param, _transaction, commandTimeout: TIMEOUT));

        public Task<IMultipleResultsProxy> QueryMultipleAsync(string sql, object? param)
            => throw new NotSupportedException("Multiple result queries within a transaction are not supported");

        public void CommitTransaction()
            => _transaction.Commit();


        private void Execute(Action toDo)
        {
            if (_useResiliency)
            {
                ResilientExecute(toDo);
            }
            else
            {
                toDo();
            }
        }
        
        private T Execute<T>(Func<T> toDo)
        {
            if (_useResiliency)
            {
                return ResilientExecute(toDo);
            }
            else
            {
                return toDo();
            }
        }

        private Task<T> ExecuteAsync<T>(Func<Task<T>> toDo)
        {
            if (_useResiliency)
            {
                return ResilientExecuteAsync(toDo);
            }
            else
            {
                return toDo();
            }
        }

        public void Dispose()
        {
            try
            {
                _connection.Dispose();
            }
            catch { }

            try
            {
                _transaction.Dispose();
            }
            catch { }
        }
    }
}
