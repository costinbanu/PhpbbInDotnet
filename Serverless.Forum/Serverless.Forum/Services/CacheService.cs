using Microsoft.Extensions.Caching.Distributed;
using Serverless.Forum.Utilities;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Serverless.Forum.Services
{
    public class CacheService
    {
        private readonly IDistributedCache _cache;
        private readonly Utils _utils;

        public CacheService(Utils utils, IDistributedCache cache)
        {
            _utils = utils;
            _cache = cache;
        }

        public async Task<T> GetFromCacheAsync<T>(string key)
            => await _utils.DecompressObjectAsync<T>(await _cache.GetAsync(key));

        public async Task SetInCacheAsync<T>(string key, T value)
            => await _cache.SetAsync(
                key,
                await _utils.CompressObjectAsync(value),
                new DistributedCacheEntryOptions { SlidingExpiration = TimeSpan.FromHours(12) }
            );

        public async Task<bool> ExistsInCacheAsync(string key)
            => (await _cache.GetAsync(key))?.Any() ?? false;

        public async Task RemoveFromCacheAsync(string key)
            => await _cache.RemoveAsync(key);
    }
}
