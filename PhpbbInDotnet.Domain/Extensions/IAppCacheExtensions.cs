using LazyCache;
using System.Threading.Tasks;

namespace PhpbbInDotnet.Domain.Extensions
{
    public static class IAppCacheExtensions
    {
        public static async Task<T> GetAndRemoveAsync<T>(this IAppCache cache, string key)
        {
            var toReturn = await cache.GetAsync<T>(key);
            cache.Remove(key);
            return toReturn;
        }
    }
}
