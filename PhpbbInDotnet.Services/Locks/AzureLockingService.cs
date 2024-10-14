using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;
using PhpbbInDotnet.Services.Storage;
using System;
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

        public async Task<(bool Success, string? LockId)> AcquireNamedLock(string name)
        {
            var blob = _blobContainerClient.GetBlobClient(name);
            if (!await blob.ExistsAsync())
            {
                await _storageService.WriteAllTextToFile(name, string.Empty);
            }
            try
            {
                var response = await blob.GetBlobLeaseClient(Guid.NewGuid().ToString()).AcquireAsync(TimeSpan.FromHours(4));
                return (true, response.Value.LeaseId);
            }
            catch (RequestFailedException rfe) when (rfe.ErrorCode == BlobErrorCode.LeaseAlreadyPresent)
            {
                return (false, null);
            }
        }

        public async Task<bool> ReleaseNamedLock(string name, string lockId)
        {
            var blob = _blobContainerClient.GetBlobClient(name);
            try
            {
                var response = await blob.GetBlobLeaseClient(lockId).ReleaseAsync();
                return true;
            }
            catch (RequestFailedException rfe) when (rfe.ErrorCode == BlobErrorCode.LeaseAlreadyPresent)
            {
                return false;
            }
        }
    }
}
