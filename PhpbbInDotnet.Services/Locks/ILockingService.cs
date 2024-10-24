using System;
using System.Threading;
using System.Threading.Tasks;

namespace PhpbbInDotnet.Services.Locks
{
    public interface ILockingService
    {
        Task<(bool Success, string? LockId, TimeSpan? Duration)> AcquireNamedLock(string name, CancellationToken cancellationToken);
        Task<bool> ReleaseNamedLock(string name, string lockId, CancellationToken cancellationToken);
        Task<bool> RenewNamedLock(string name, string lockId, CancellationToken cancellationToken);
    }
}