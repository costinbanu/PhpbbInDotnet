using System.Threading.Tasks;

namespace PhpbbInDotnet.Services.Locks
{
    public interface ILockingService
    {
        Task<(bool Success, string? LockId)> AcquireNamedLock(string name);
        Task<bool> ReleaseNamedLock(string name, string lockId);
    }
}