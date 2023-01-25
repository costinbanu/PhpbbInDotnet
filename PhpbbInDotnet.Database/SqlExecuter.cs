using Dapper;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;

namespace PhpbbInDotnet.Database
{
    class SqlExecuter : ISqlExecuter
    {
        private readonly IDbConnection _connection;

        public SqlExecuter(IForumDbContext forumDbContext)
        {
            _connection = forumDbContext.Database.GetDbConnection();
        }

        public Task<int> ExecuteAsync(string sql, object? param)
            => _connection.ExecuteAsync(sql, param);

        public T ExecuteScalar<T>(string sql, object? param)
            => _connection.ExecuteScalar<T>(sql, param);

        public Task<T> ExecuteScalarAsync<T>(string sql, object? param)
            => _connection.ExecuteScalarAsync<T>(sql, param);

        public IEnumerable<T> Query<T>(string sql, object? param)
            => _connection.Query<T>(sql, param);

        public IEnumerable<dynamic> Query(string sql, object? param)
            => _connection.Query(sql, param);

        public Task<IEnumerable<T>> QueryAsync<T>(string sql, object? param)
            => _connection.QueryAsync<T>(sql, param);

        public Task<IEnumerable<dynamic>> QueryAsync(string sql, object? param)
            => _connection.QueryAsync(sql, param);

        public T QueryFirstOrDefault<T>(string sql, object? param)
            => _connection.QueryFirstOrDefault<T>(sql, param);

        public Task<T> QueryFirstOrDefaultAsync<T>(string sql, object? param)
            => _connection.QueryFirstOrDefaultAsync<T>(sql, param);

        public Task<dynamic> QueryFirstOrDefaultAsync(string sql, object? param)
            => _connection.QueryFirstOrDefaultAsync(sql, param);

        public Task<T> QuerySingleOrDefaultAsync<T>(string sql, object? param)
            => _connection.QuerySingleOrDefaultAsync<T>(sql, param);

        public Task<T> QuerySingleAsync<T>(string sql, object? param)
            => _connection.QuerySingleAsync<T>(sql, param);

        public Task<dynamic> QuerySingleOrDefaultAsync(string sql, object? param)
            => _connection.QuerySingleOrDefaultAsync(sql, param);
    }
}
