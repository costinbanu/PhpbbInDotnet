using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;
using PhpbbInDotnet.Services.Storage;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace PhpbbInDotnet.Services.Locks
{
    class AzureLockingService : ILockingService
    {
        private readonly BlobContainerClient _blobContainerClient;
        private readonly IStorageService _storageService;

        public AzureLockingService(BlobContainerClient blobContainerClient, IStorageService storageService)
        {
            _blobContainerClient = blobContainerClient;
            _storageService = storageService;
        }

        public async Task<(bool Success, string? LockId, TimeSpan? Duration)> AcquireNamedLock(string name, CancellationToken cancellationToken)
        {
            var duration = TimeSpan.FromMinutes(1);
            var blob = _blobContainerClient.GetBlobClient(name);
            if (!await blob.ExistsAsync(cancellationToken))
            {
                await _storageService.WriteAllTextToFile(name, string.Empty);
            }
            try
            {
                var response = await blob.GetBlobLeaseClient(Guid.NewGuid().ToString()).AcquireAsync(duration: duration, cancellationToken: cancellationToken);
                return (true, response.Value.LeaseId, duration);
            }
            catch (RequestFailedException rfe) when (rfe.ErrorCode == BlobErrorCode.LeaseAlreadyPresent)
            {
                return (false, null, null);
            }
        }

        public async Task<bool> ReleaseNamedLock(string name, string lockId, CancellationToken cancellationToken)
        {
            var blob = _blobContainerClient.GetBlobClient(name);
            try
            {
                var response = await blob.GetBlobLeaseClient(lockId).ReleaseAsync(cancellationToken: cancellationToken);
                return true;
            }
            catch (RequestFailedException rfe) when (rfe.ErrorCode == BlobErrorCode.LeaseAlreadyPresent)
            {
                return false;
            }
        }

        public async Task<bool> RenewNamedLock(string name, string lockId, CancellationToken cancellationToken)
        {
            var blob = _blobContainerClient.GetBlobClient(name);
            try
            {
                var response = await blob.GetBlobLeaseClient(lockId).RenewAsync(cancellationToken: cancellationToken);
                return true;
            }
            catch (RequestFailedException)
            {
                return false;
            }
        }
    }
}
