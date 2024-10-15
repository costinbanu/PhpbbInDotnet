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

        public Task<(bool Success, string? LockId, TimeSpan? Duration)> AcquireNamedLock(string name, CancellationToken _)
        {
            try
            {
                _lock.EnterWriteLock();
                var id = Guid.NewGuid().ToString();
                if (_locks.TryAdd(name, id))
                {
                    return Task.FromResult<(bool Success, string? LockId, TimeSpan? Duration)>((true, id, TimeSpan.FromMinutes(1)));
                }
                return Task.FromResult<(bool Success, string? LockId, TimeSpan? Duration)>((false, null, null));
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        public Task<bool> ReleaseNamedLock(string name, string lockId, CancellationToken __)
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

        public Task<bool> RenewNamedLock(string name, string lockId, CancellationToken _)
            => Task.FromResult(true);
    }
}
