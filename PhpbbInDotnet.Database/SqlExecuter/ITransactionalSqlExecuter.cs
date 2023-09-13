using System;

namespace PhpbbInDotnet.Database.SqlExecuter
{
    public interface ITransactionalSqlExecuter : ISqlExecuter, IDisposable
    {
        void CommitTransaction();
    }
}
