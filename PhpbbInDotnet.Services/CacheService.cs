using Microsoft.Extensions.Caching.Distributed;
using PhpbbInDotnet.Utilities;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace PhpbbInDotnet.Services
{
    public class CacheService
    {
        private readonly IDistributedCache _cache;
        private readonly CommonUtils _utils;

        public CacheService(CommonUtils utils, IDistributedCache cache)
        {
            _utils = utils;
            _cache = cache;
        }

        public async Task<T> GetFromCache<T>(string key)
            => await _utils.DecompressObject<T>(await _cache.GetAsync(key));

        public async Task<T> GetAndRemoveFromCache<T>(string key)
        {
            var toReturn = await GetFromCache<T>(key);
            await RemoveFromCache(key);
            return toReturn;
        }

        public async Task SetInCache<T>(string key, T value, TimeSpan? expiration = null, bool absoluteExpiration = false)
        {
            DistributedCacheEntryOptions options;
            if (absoluteExpiration)
            {
                options = new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = expiration ?? TimeSpan.FromHours(4) };
            }
            else
            {
                options = new DistributedCacheEntryOptions { SlidingExpiration = expiration ?? TimeSpan.FromHours(4) };
            }
            await _cache.SetAsync(key, await _utils.CompressObject(value), options);
        }

        public async Task<bool> ExistsInCache(string key)
            => (await _cache.GetAsync(key))?.Any() ?? false;

        public async Task RemoveFromCache(string key)
            => await _cache.RemoveAsync(key);
    }
}
