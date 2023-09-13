using Dapper;
using PhpbbInDotnet.Domain;
using System;
using System.Collections.Generic;
using System.Data;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace PhpbbInDotnet.Database.SqlExecuter
{
    class PaginatedDapperProxy : IDapperProxy
    {
        private readonly DapperProxy _implementation;
        private readonly DatabaseType _databaseType;
        private readonly int _skip;
        private readonly int _take;
        private readonly IDbTransaction? _transaction;

        internal const string PAGINATION_WILDCARD = "##paginate";

        private static readonly Regex WILDCARD = new(PAGINATION_WILDCARD, RegexOptions.Compiled, Constants.REGEX_TIMEOUT);

        internal PaginatedDapperProxy(DapperProxy implementation, DatabaseType databaseType, int skip, int take, IDbTransaction? transaction)
        {
            _implementation = implementation;
            _databaseType = databaseType;
            _skip = skip;
            _take = take;
            _transaction = transaction;
        }

        public Task<int> ExecuteAsync(string sql, object? param)
            => _implementation.ExecuteAsyncImpl(AdjustSql(sql), AdjustParameters(param), _transaction);

        public T ExecuteScalar<T>(string sql, object? param)
            => _implementation.ExecuteScalarImpl<T>(AdjustSql(sql), AdjustParameters(param), _transaction);

        public Task<T> ExecuteScalarAsync<T>(string sql, object? param)
            => _implementation.ExecuteScalarAsyncImpl<T>(AdjustSql(sql), AdjustParameters(param), _transaction);

        public IEnumerable<T> Query<T>(string sql, object? param)
            => _implementation.QueryImpl<T>(AdjustSql(sql), AdjustParameters(param), _transaction);

        public IEnumerable<dynamic> Query(string sql, object? param)
            => _implementation.QueryImpl(AdjustSql(sql), AdjustParameters(param), _transaction);

        public Task<IEnumerable<T>> QueryAsync<T>(string sql, object? param)
            => _implementation.QueryAsyncImpl<T>(AdjustSql(sql), AdjustParameters(param), _transaction);

        public Task<IEnumerable<dynamic>> QueryAsync(string sql, object? param)
            => _implementation.QueryAsyncImpl(AdjustSql(sql), AdjustParameters(param), _transaction);

        public T QueryFirstOrDefault<T>(string sql, object? param)
            => _implementation.QueryFirstOrDefaultImpl<T>(AdjustSql(sql), AdjustParameters(param), _transaction);

        public Task<T> QueryFirstOrDefaultAsync<T>(string sql, object? param)
            => _implementation.QueryFirstOrDefaultAsyncImpl<T>(AdjustSql(sql), AdjustParameters(param), _transaction);

        public Task<dynamic> QueryFirstOrDefaultAsync(string sql, object? param)
            => _implementation.QueryFirstOrDefaultAsyncImpl(AdjustSql(sql), AdjustParameters(param), _transaction);

        public T QuerySingle<T>(string sql, object? param)
            => _implementation.QuerySingleImpl<T>(AdjustSql(sql), AdjustParameters(param), _transaction);

        public Task<T> QuerySingleAsync<T>(string sql, object? param)
            => _implementation.QuerySingleAsyncImpl<T>(AdjustSql(sql), AdjustParameters(param), _transaction);

        public Task<T> QuerySingleOrDefaultAsync<T>(string sql, object? param)
            => _implementation.QuerySingleOrDefaultAsyncImpl<T>(AdjustSql(sql), AdjustParameters(param), _transaction);

        public Task<dynamic> QuerySingleOrDefaultAsync(string sql, object? param)
            => _implementation.QuerySingleOrDefaultAsyncImpl(AdjustSql(sql), AdjustParameters(param), _transaction);

        public Task<SqlMapper.GridReader> QueryMultipleAsync(string sql, object? param)
            => _implementation.QueryMultipleAsyncImpl(AdjustSql(sql), AdjustParameters(param), _transaction);

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
