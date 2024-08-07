﻿using Dapper;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;

namespace PhpbbInDotnet.Database.SqlExecuter
{
    public interface ISqlExecuter : IDapperProxy
    {
        string LastInsertedItemId { get; }
        string PaginationWildcard { get; }
        IEnumerable<T> CallStoredProcedure<T>(string storedProcedureName, object? param = null);
        Task<IEnumerable<T>> CallStoredProcedureAsync<T>(string storedProcedureName, object? param = null);
        Task CallStoredProcedureAsync(string storedProcedureName, object? param = null);
        Task<IMultipleResultsProxy> CallMultipleResultsStoredProcedureAsync(string storedProcedureName, object? param);
        IDapperProxy WithPagination(int skip, int take);
        ITransactionalSqlExecuter BeginTransaction();
		ITransactionalSqlExecuter BeginTransaction(IsolationLevel isolationLevel);
	}
}