using Dapper;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PhpbbInDotnet.Database.SqlExecuter
{
    public interface ISqlExecuter : IDapperProxy
    {
        string LastInsertedItemId { get; }
        IEnumerable<T> CallStoredProcedure<T>(string storedProcedureName, object? param = null);
        Task<IEnumerable<T>> CallStoredProcedureAsync<T>(string storedProcedureName, object? param = null);
        Task CallStoredProcedureAsync(string storedProcedureName, object? param = null);
        Task<SqlMapper.GridReader> CallMultipleResultsStoredProcedureAsync(string storedProcedureName, object? param);
        IDapperProxy WithPagination(int skip, int take);
	}
}