using PhpbbInDotnet.Domain;
using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace PhpbbInDotnet.Database.SqlExecuter
{
	partial class SqlExecuter
	{
		class PaginatedDapperProxy : IDapperProxy
		{
			private readonly IDapperProxy _implementation;
			private readonly DatabaseType _databaseType;
			private readonly int _skip;
			private readonly int _take;

			private static readonly Regex WILDCARD = new("##paginate", RegexOptions.Compiled, Constants.REGEX_TIMEOUT);

			internal PaginatedDapperProxy(IDapperProxy implementation, DatabaseType databaseType, int skip, int take)
			{
				_implementation = implementation;
				_databaseType = databaseType;
				_skip = skip;
				_take = take;
			}

			public Task<int> ExecuteAsync(string sql, object? param = null)
				=> _implementation.ExecuteAsync(AdjustSql(sql), AdjustParameters(param));

			public T ExecuteScalar<T>(string sql, object? param = null)
				=> _implementation.ExecuteScalar<T>(AdjustSql(sql), AdjustParameters(param));

			public Task<T> ExecuteScalarAsync<T>(string sql, object? param = null)
				=> _implementation.ExecuteScalarAsync<T>(AdjustSql(sql), AdjustParameters(param));

			public IEnumerable<T> Query<T>(string sql, object? param = null)
				=> _implementation.Query<T>(AdjustSql(sql), AdjustParameters(param));

			public IEnumerable<dynamic> Query(string sql, object? param = null)
				=> _implementation.Query(AdjustSql(sql), AdjustParameters(param));

			public Task<IEnumerable<T>> QueryAsync<T>(string sql, object? param = null)
				=> _implementation.QueryAsync<T>(AdjustSql(sql), AdjustParameters(param));

			public Task<IEnumerable<dynamic>> QueryAsync(string sql, object? param = null)
				=> _implementation.QueryAsync(AdjustSql(sql), AdjustParameters(param));

			public T QueryFirstOrDefault<T>(string sql, object? param = null)
				=> _implementation.QueryFirstOrDefault<T>(AdjustSql(sql), AdjustParameters(param));

			public Task<T> QueryFirstOrDefaultAsync<T>(string sql, object? param = null)
				=> _implementation.QueryFirstOrDefaultAsync<T>(AdjustSql(sql), AdjustParameters(param));

			public Task<dynamic> QueryFirstOrDefaultAsync(string sql, object? param = null)
				=> _implementation.QueryFirstOrDefaultAsync(AdjustSql(sql), AdjustParameters(param));

			public T QuerySingle<T>(string sql, object? param)
				=> _implementation.QuerySingle<T>(AdjustSql(sql), AdjustParameters(param));

			public Task<T> QuerySingleAsync<T>(string sql, object? param)
				=> _implementation.QuerySingleAsync<T>(AdjustSql(sql), AdjustParameters(param));

			public Task<T> QuerySingleOrDefaultAsync<T>(string sql, object? param = null)
				=> _implementation.QuerySingleOrDefaultAsync<T>(AdjustSql(sql), AdjustParameters(param));

			public Task<dynamic> QuerySingleOrDefaultAsync(string sql, object? param = null)
				=> _implementation.QuerySingleOrDefaultAsync(AdjustSql(sql), AdjustParameters(param));

			private string AdjustSql(string sql)
			{
				var stmt = _databaseType switch
				{
					DatabaseType.MySql => "LIMIT @skip, @take",
					DatabaseType.SqlServer => "OFFSET @skip ROWS FETCH NEXT @take ROWS ONLY",
					_ => throw new ArgumentException("Unknown Database type in configuration.")
				};

				if (WILDCARD.IsMatch(sql))
				{
					return WILDCARD.Replace(sql, stmt);
				}
				
				return $"{sql.TrimEnd().TrimEnd(';')}{Environment.NewLine}{stmt};";
			}

			private object AdjustParameters(object? param)
			{
				if (param is null)
				{
					return new
					{
						skip = _skip,
						take = _take,
					};
				}

				dynamic expando = new ExpandoObject();
				var result = expando as IDictionary<string, object>;
				foreach (System.Reflection.PropertyInfo fi in param.GetType().GetProperties())
				{
					result![fi.Name] = fi.GetValue(param, null)!;
				}
				result!["skip"] = _skip;
				result["take"] = _take;
				return result;
			}
		}
	}
}
