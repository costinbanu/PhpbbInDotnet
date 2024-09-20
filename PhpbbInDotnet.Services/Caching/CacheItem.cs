using LazyCache;
using System;
using System.Threading.Tasks;

namespace PhpbbInDotnet.Services.Caching
{
	public class CacheItem<TItem>
	{
		private readonly Func<Task<TItem>> _factory;
		private readonly string _key;
		private readonly IAppCache _cache;
		private readonly TimeSpan _timeout;

		internal CacheItem(Func<Task<TItem>> factory, string key, IAppCache cache)
		{
			_factory = factory;
			_key = key;
			_cache = cache;
			_timeout = TimeSpan.FromMinutes(30);
		}

		public Task<TItem> Get()
			=> _cache.GetOrAddAsync(_key, _factory, DateTimeOffset.UtcNow + _timeout);

		public void Invalidate()
			=> _cache.Remove(_key);
	}
}
