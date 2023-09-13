using Dapper;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;

namespace PhpbbInDotnet.Database.SqlExecuter
{
    public interface IDapperProxy
	{
		Task<IEnumerable<T>> QueryAsync<T>(string sql, object? param = null);
		IEnumerable<T> Query<T>(string sql, object? param = null);
		Task<IEnumerable<dynamic>> QueryAsync(string sql, object? param = null);
		IEnumerable<dynamic> Query(string sql, object? param = null);
		Task<T> QueryFirstOrDefaultAsync<T>(string sql, object? param = null);
		T QueryFirstOrDefault<T>(string sql, object? param = null);
		Task<dynamic> QueryFirstOrDefaultAsync(string sql, object? param = null);
		Task<T> QuerySingleOrDefaultAsync<T>(string sql, object? param = null);
		Task<T> QuerySingleAsync<T>(string sql, object? param);
		T QuerySingle<T>(string sql, object? param);
		Task<dynamic> QuerySingleOrDefaultAsync(string sql, object? param = null);
		Task<T> ExecuteScalarAsync<T>(string sql, object? param = null);
		T ExecuteScalar<T>(string sql, object? param = null);
		Task<int> ExecuteAsync(string sql, object? param = null);
		Task<SqlMapper.GridReader> QueryMultipleAsync(string sql, object? param);
    }
}
