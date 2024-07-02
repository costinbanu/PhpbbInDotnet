using Dapper;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;

namespace PhpbbInDotnet.Database.SqlExecuter
{
    class MultipleResultsProxy(IDbConnection dbConnection, SqlMapper.GridReader reader) : IMultipleResultsProxy
    {
        private readonly IDbConnection _dbConnection = dbConnection;
        private readonly SqlMapper.GridReader _reader = reader;

        public void Dispose()
        {
            try
            {
                _dbConnection.Dispose();
            }
            catch { }

            try
            {
                _reader.Dispose();
            }
            catch { }
        }

        public Task<IEnumerable<T>> ReadAsync<T>()
            => _reader.ReadAsync<T>();

        public Task<T?> ReadFirstOrDefaultAsync<T>()
            => _reader.ReadFirstOrDefaultAsync<T>();
    }
}
