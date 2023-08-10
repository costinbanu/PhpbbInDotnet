using Dapper;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;

namespace PhpbbInDotnet.Database.SqlExecuter
{
    public interface ISqlExecuter : IDapperProxy
    {
        string LastInsertedItemId { get; }
        string PaginationWildcard { get; }
        IEnumerable<T> CallStoredProcedure<T>(string storedProcedureName, object? param = null, IDbTransaction? dbTransaction = null);
        Task<IEnumerable<T>> CallStoredProcedureAsync<T>(string storedProcedureName, object? param = null, IDbTransaction? dbTransaction = null);
        Task CallStoredProcedureAsync(string storedProcedureName, object? param = null, IDbTransaction? dbTransaction = null);
        Task<SqlMapper.GridReader> CallMultipleResultsStoredProcedureAsync(string storedProcedureName, object? param, IDbTransaction? dbTransaction = null);
        IDapperProxy WithPagination(int skip, int take);
	}
}