using System;
using System.Threading.Tasks;

namespace PhpbbInDotnet.Database.SqlExecuter
{
    public interface ITransactionalSqlExecuter : ISqlExecuter, IDisposable
    {
        Task CommitTransaction();

        Func<Task> OnSuccessfulCommit { get; set; }
    }
}
