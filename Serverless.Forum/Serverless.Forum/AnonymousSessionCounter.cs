using Microsoft.Extensions.Caching.Memory;
using System;

namespace Serverless.Forum
{
    public class AnonymousSessionCounter
    {
        private AnonymousSessionCounter()
        {
            _cache = new MemoryCache(new MemoryCacheOptions());
        }

        private static AnonymousSessionCounter instance;

        public static AnonymousSessionCounter Instance => instance ??= new AnonymousSessionCounter();

        public void UpsertSession(string sessionId, int expirationInMinutes)
        {
            _cache.Set(sessionId, new object(), TimeSpan.FromMinutes(expirationInMinutes));
        }

        public int GetActiveSessionCount()
            => _cache.Count;

        private readonly MemoryCache _cache;
    }
}
