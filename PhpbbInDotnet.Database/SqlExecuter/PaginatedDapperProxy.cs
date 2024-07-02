using Dapper;
using Microsoft.Extensions.Configuration;
using PhpbbInDotnet.Domain;
using Serilog;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace PhpbbInDotnet.Database.SqlExecuter
{
    sealed class PaginatedDapperProxy(int skip, int take, IConfiguration configuration, ILogger logger) : DapperProxy(configuration, logger)
    {
        private readonly int _skip = skip;
        private readonly int _take = take;

        private static readonly Regex WILDCARD = new(PAGINATION_WILDCARD, RegexOptions.Compiled, Constants.REGEX_TIMEOUT);

        public override Task<int> ExecuteAsync(string sql, object? param)
            => base.ExecuteAsync(AdjustSql(sql), AdjustParameters(param));

        public override T? ExecuteScalar<T>(string sql, object? param) where T : default
            => base.ExecuteScalar<T>(AdjustSql(sql), AdjustParameters(param));

        public override Task<T?> ExecuteScalarAsync<T>(string sql, object? param) where T : default
            => base.ExecuteScalarAsync<T>(AdjustSql(sql), AdjustParameters(param));

        public override IEnumerable<T> Query<T>(string sql, object? param)
            => base.Query<T>(AdjustSql(sql), AdjustParameters(param));

        public override IEnumerable<dynamic> Query(string sql, object? param)
            => base.Query(AdjustSql(sql), AdjustParameters(param));

        public override Task<IEnumerable<T>> QueryAsync<T>(string sql, object? param)
            => base.QueryAsync<T>(AdjustSql(sql), AdjustParameters(param));

        public override Task<IEnumerable<dynamic>> QueryAsync(string sql, object? param)
            => base.QueryAsync(AdjustSql(sql), AdjustParameters(param));

        public override T? QueryFirstOrDefault<T>(string sql, object? param) where T : default
            => base.QueryFirstOrDefault<T>(AdjustSql(sql), AdjustParameters(param));

        public override Task<T?> QueryFirstOrDefaultAsync<T>(string sql, object? param) where T : default
            => base.QueryFirstOrDefaultAsync<T>(AdjustSql(sql), AdjustParameters(param));

        public override Task<dynamic?> QueryFirstOrDefaultAsync(string sql, object? param)
            => base.QueryFirstOrDefaultAsync(AdjustSql(sql), AdjustParameters(param));

        public override T QuerySingle<T>(string sql, object? param)
            => base.QuerySingle<T>(AdjustSql(sql), AdjustParameters(param));

        public override Task<T> QuerySingleAsync<T>(string sql, object? param)
            => base.QuerySingleAsync<T>(AdjustSql(sql), AdjustParameters(param));

        public override Task<T?> QuerySingleOrDefaultAsync<T>(string sql, object? param) where T : default
            => base.QuerySingleOrDefaultAsync<T>(AdjustSql(sql), AdjustParameters(param));

        public override Task<dynamic?> QuerySingleOrDefaultAsync(string sql, object? param)
            => base.QuerySingleOrDefaultAsync(AdjustSql(sql), AdjustParameters(param));

        public override Task<SqlMapper.GridReader> QueryMultipleAsync(string sql, object? param)
            => base.QueryMultipleAsync(AdjustSql(sql), AdjustParameters(param));

        public override Task<int> ExecuteAsyncWithoutResiliency(string sql, object? param = null, int commandTimeout = DapperProxy.TIMEOUT)
            => base.ExecuteAsyncWithoutResiliency(sql, param, commandTimeout);

        private string AdjustSql(string sql)
        {
            var stmt = DatabaseType switch
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
