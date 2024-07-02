using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PhpbbInDotnet.Database.SqlExecuter
{
    public interface IMultipleResultsProxy : IDisposable
    {
        Task<IEnumerable<T>> ReadAsync<T>();
        Task<T?> ReadFirstOrDefaultAsync<T>();
    }
}
