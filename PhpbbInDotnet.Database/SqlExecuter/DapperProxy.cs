using Dapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Serilog;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PhpbbInDotnet.Database.SqlExecuter
{
    abstract class DapperProxy(IConfiguration configuration, ILogger logger) : BaseDapperProxy(configuration, logger), IDapperProxy
    {
        public virtual Task<int> ExecuteAsyncWithoutResiliency(string sql, object? param = null, int commandTimeout = TIMEOUT)
        {
            using var connection = GetDbConnection();
            return connection.ExecuteAsync(sql, param, commandTimeout: commandTimeout);
        }

        public virtual Task<int> ExecuteAsync(string sql, object? param)
            => ResilientExecuteAsync(() =>
            {
                using var connection = GetDbConnection();
                return connection.ExecuteAsync(sql, param, commandTimeout: TIMEOUT);
            });

        public virtual T? ExecuteScalar<T>(string sql, object? param)
            => ResilientExecute(() =>
            {
                using var connection = GetDbConnection();
                return connection.ExecuteScalar<T>(sql, param, commandTimeout: TIMEOUT);
            });

        public virtual Task<T?> ExecuteScalarAsync<T>(string sql, object? param)
            => ResilientExecuteAsync(() =>
            {
                using var connection = GetDbConnection();
                return connection.ExecuteScalarAsync<T>(sql, param, commandTimeout: TIMEOUT);
            });

        public virtual IEnumerable<dynamic> Query(string sql, object? param)
            => ResilientExecute(() =>
            {
                using var connection = GetDbConnection();
                return connection.Query(sql, param, commandTimeout: TIMEOUT);
            });

        public virtual IEnumerable<T> Query<T>(string sql, object? param)
            => ResilientExecute(() =>
            {
                using var connection = GetDbConnection();
                return connection.Query<T>(sql, param, commandTimeout: TIMEOUT);
            });

        public virtual Task<IEnumerable<dynamic>> QueryAsync(string sql, object? param)
            => ResilientExecuteAsync(() =>
            {
                using var connection = GetDbConnection();
                return connection.QueryAsync(sql, param, commandTimeout: TIMEOUT);
            });

        public virtual Task<IEnumerable<T>> QueryAsync<T>(string sql, object? param)
            => ResilientExecuteAsync(() =>
            {
                using var connection = GetDbConnection();
                return connection.QueryAsync<T>(sql, param, commandTimeout: TIMEOUT);
            });

        public virtual T? QueryFirstOrDefault<T>(string sql, object? param)
            => ResilientExecute(() =>
            {
                using var connection = GetDbConnection();
                return connection.QueryFirstOrDefault<T>(sql, param, commandTimeout: TIMEOUT);
            });

        public virtual Task<dynamic?> QueryFirstOrDefaultAsync(string sql, object? param)
            => ResilientExecuteAsync(() =>
            {
                using var connection = GetDbConnection();
                return connection.QueryFirstOrDefaultAsync(sql, param, commandTimeout: TIMEOUT);
            });

        public virtual Task<T?> QueryFirstOrDefaultAsync<T>(string sql, object? param)
            => ResilientExecuteAsync(() =>
            {
                using var connection = GetDbConnection();
                return connection.QueryFirstOrDefaultAsync<T>(sql, param, commandTimeout: TIMEOUT);
            });

        public virtual T QuerySingle<T>(string sql, object? param)
            => ResilientExecute(() =>
            {
                using var connection = GetDbConnection();
                return connection.QuerySingle<T>(sql, param, commandTimeout: TIMEOUT);
            });

        public virtual Task<T> QuerySingleAsync<T>(string sql, object? param)
            => ResilientExecuteAsync(() =>
            {
                using var connection = GetDbConnection();
                return connection.QuerySingleAsync<T>(sql, param, commandTimeout: TIMEOUT);
            });

        public virtual Task<dynamic?> QuerySingleOrDefaultAsync(string sql, object? param)
            => ResilientExecuteAsync(() =>
            {
                using var connection = GetDbConnection();
                return connection.QuerySingleOrDefaultAsync(sql, param, commandTimeout: TIMEOUT);
            });

        public virtual Task<T?> QuerySingleOrDefaultAsync<T>(string sql, object? param)
            => ResilientExecuteAsync(() =>
            {
                using var connection = GetDbConnection();
                return connection.QuerySingleOrDefaultAsync<T>(sql, param, commandTimeout: TIMEOUT);
            });

        public virtual Task<SqlMapper.GridReader> QueryMultipleAsync(string sql, object? param)
            => ResilientExecuteAsync(() =>
            {
                using var connection = GetDbConnection();
                return connection.QueryMultipleAsync(sql, param, commandTimeout: TIMEOUT);
            });
    }
}