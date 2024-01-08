using Dapper;
using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;

namespace PhpbbInDotnet.Database.SqlExecuter
{
    class TransactionalSqlExecuter : ITransactionalSqlExecuter
    {
        private readonly IDbTransaction _transaction;
        private readonly SqlExecuter _implementation;

        internal TransactionalSqlExecuter(IDbTransaction transaction, SqlExecuter implementation)
        {
            _transaction = transaction;
            _implementation = implementation;
        }

        public string LastInsertedItemId => _implementation.LastInsertedItemId;

        public string PaginationWildcard => _implementation.PaginationWildcard;

        public ITransactionalSqlExecuter BeginTransaction(IsolationLevel _)
        {
            throw new NotSupportedException("Transaction already started");
        }

		public ITransactionalSqlExecuter BeginTransaction()
		{
			throw new NotSupportedException("Transaction already started");
		}

		public Task<SqlMapper.GridReader> CallMultipleResultsStoredProcedureAsync(string storedProcedureName, object? param)
            => _implementation.CallMultipleResultsStoredProcedureAsyncImpl(storedProcedureName, param, _transaction);

        public IEnumerable<T> CallStoredProcedure<T>(string storedProcedureName, object? param = null)
            => _implementation.CallStoredProcedureImpl<T>(storedProcedureName, param, _transaction);

        public Task<IEnumerable<T>> CallStoredProcedureAsync<T>(string storedProcedureName, object? param = null)
            => _implementation.CallStoredProcedureAsyncImpl<T>(storedProcedureName, param, _transaction);

        public Task CallStoredProcedureAsync(string storedProcedureName, object? param = null)
            => _implementation.CallStoredProcedureAsyncImpl(storedProcedureName, param, _transaction);

        public void CommitTransaction()
            => _transaction.Commit();

        public void Dispose()
            => _transaction.Dispose();

        public Task<int> ExecuteAsync(string sql, object? param = null)
            => _implementation.ExecuteAsyncImpl(sql, param, _transaction);

        public T ExecuteScalar<T>(string sql, object? param = null)
            => _implementation.ExecuteScalarImpl<T>(sql, param, _transaction);

        public Task<T> ExecuteScalarAsync<T>(string sql, object? param = null)
            => _implementation.ExecuteScalarAsyncImpl<T>(sql, param, _transaction);

        public IEnumerable<T> Query<T>(string sql, object? param = null)
        => _implementation.QueryImpl<T>(sql, param, _transaction);

        public IEnumerable<dynamic> Query(string sql, object? param = null)
            => _implementation.QueryImpl(sql, param, _transaction);

        public Task<IEnumerable<T>> QueryAsync<T>(string sql, object? param = null)
            => _implementation.QueryAsyncImpl<T>(sql, param, _transaction);

        public Task<IEnumerable<dynamic>> QueryAsync(string sql, object? param = null)
            => _implementation.QueryAsyncImpl(sql, param, _transaction);

        public T QueryFirstOrDefault<T>(string sql, object? param = null)
            => _implementation.QueryFirstOrDefaultImpl<T>(sql, param, _transaction);

        public Task<T> QueryFirstOrDefaultAsync<T>(string sql, object? param = null)
            => _implementation.QueryFirstOrDefaultAsyncImpl<T>(sql, param, _transaction);

        public Task<dynamic> QueryFirstOrDefaultAsync(string sql, object? param = null)
            => _implementation.QueryFirstOrDefaultAsyncImpl(sql, param, _transaction);

        public Task<SqlMapper.GridReader> QueryMultipleAsync(string sql, object? param)
            => _implementation.QueryMultipleAsyncImpl(sql, param, _transaction);

        public T QuerySingle<T>(string sql, object? param)
            => _implementation.QuerySingleImpl<T>(sql, param, _transaction);

        public Task<T> QuerySingleAsync<T>(string sql, object? param)
            => _implementation.QuerySingleAsyncImpl<T>(sql, param, _transaction);

        public Task<T> QuerySingleOrDefaultAsync<T>(string sql, object? param = null)
            => _implementation.QuerySingleOrDefaultAsyncImpl<T>(sql, param, _transaction);

        public Task<dynamic> QuerySingleOrDefaultAsync(string sql, object? param = null)
            => _implementation.QuerySingleOrDefaultAsyncImpl(sql, param, _transaction);

        public Task<int> ExecuteAsyncWithoutResiliency(string sql, object? param = null, int commandTimeout = DapperProxy.TIMEOUT)
            => _implementation.ExecuteAsyncWithoutResiliency(sql, param, commandTimeout);

        public IDapperProxy WithPagination(int skip, int take)
            => new PaginatedDapperProxy(_implementation, _implementation.DatabaseType, skip, take, _transaction);
    }
}
