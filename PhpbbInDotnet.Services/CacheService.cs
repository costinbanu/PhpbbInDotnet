using LazyCache;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using PhpbbInDotnet.Utilities;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace PhpbbInDotnet.Services
{
    public class CacheService
    {
        private readonly IAppCache _cache;
        private readonly CommonUtils _utils;

        public CacheService(CommonUtils utils, IAppCache cache)
        {
            _utils = utils;
            _cache = cache;
        }

        public async Task<T> GetFromCache<T>(string key)
            => await _utils.DecompressObject<T>(await _cache.GetAsync<byte[]>(key));

        public async Task<T> GetAndRemoveFromCache<T>(string key)
        {
            var toReturn = await GetFromCache<T>(key);
            RemoveFromCache(key);
            return toReturn;
        }

        public async Task SetInCache<T>(string key, T value, TimeSpan? expiration = null, bool absoluteExpiration = false)
        {
            MemoryCacheEntryOptions options;
            if (absoluteExpiration)
            {
                options = new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = expiration ?? TimeSpan.FromHours(4) };
            }
            else
            {
                options = new MemoryCacheEntryOptions { SlidingExpiration = expiration ?? TimeSpan.FromHours(4) };
            }
            _cache.Add(key, await _utils.CompressObject(value), options);
        }

        public async Task<bool> ExistsInCache(string key)
            => (await _cache.GetAsync<object>(key)) != null;

        public void RemoveFromCache(string key)
            => _cache.Remove(key);
    }
}
