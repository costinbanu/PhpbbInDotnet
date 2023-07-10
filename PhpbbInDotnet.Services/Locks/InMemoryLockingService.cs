using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace PhpbbInDotnet.Services.Locks
{
    class InMemoryLockingService : ILockingService
    {
        private static readonly ReaderWriterLockSlim _lock = new();
        private static readonly ConcurrentDictionary<string, string> _locks = new();

        public Task<(bool Success, string? LockId)> AcquireNamedLock(string name)
        {
            try
            {
                _lock.EnterWriteLock();
                var id = Guid.NewGuid().ToString();
                if (_locks.TryAdd(name, id))
                {
                    return Task.FromResult<(bool Success, string? LockId)>((true, id));
                }
                return Task.FromResult<(bool Success, string? LockId)>((false, null));
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        public Task<bool> ReleaseNamedLock(string name, string lockId)
        {
            try
            {
                _lock.EnterWriteLock();
                if (_locks.TryGetValue(name, out var val) && val == lockId && _locks.TryRemove(name, out _))
                {
                    return Task.FromResult(true);
                }
                return Task.FromResult(false);
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }
    }
}
