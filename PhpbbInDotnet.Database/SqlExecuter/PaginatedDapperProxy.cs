using Dapper;
using PhpbbInDotnet.Domain;
using PhpbbInDotnet.Domain.Utilities;
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

			private DynamicParameters AdjustParameters(object? param)
			{
				var result = new DynamicParameters();
				if (param is not null)
				{
					result.AddDynamicParams(param);
				}
				result.Add("skip", _skip);
				result.Add("take", _take);
				return result;
			}
		}
	}
}
