using Microsoft.Extensions.Caching.Distributed;
using PhpbbInDotnet.Domain.Utilities;
using Serilog;
using System;
using System.Threading.Tasks;

namespace PhpbbInDotnet.Services.Caching
{
	public class CacheItem<TItem>
	{
		private readonly Func<Task<TItem>> _factory;
		private readonly string _key;
		private readonly IDistributedCache _cache;
		private readonly ILogger _logger;
		private readonly TimeSpan _timeout;

		internal CacheItem(Func<Task<TItem>> factory, string key, IDistributedCache cache, ILogger logger)
		{
			_factory = factory;
			_key = key;
			_cache = cache;
			_logger = logger;
			_timeout = TimeSpan.FromMinutes(30);
		}

		public async Task<TItem> GetAsync()
		{
			var bytes = await _cache.GetAsync(_key);
			if (bytes?.Length > 0)
			{
				var obj = await CompressionUtility.DecompressObject<TItem>(bytes);
				if (obj is not null)
				{
					return obj;
				}
			}

			var toReturn = await _factory();
			try
			{
				bytes = await CompressionUtility.CompressObject(toReturn);
				var opts = new DistributedCacheEntryOptions()
				{
					AbsoluteExpiration = DateTimeOffset.UtcNow + _timeout,
				};
				await _cache.SetAsync(_key, bytes, opts);
			}
			catch (Exception ex)
			{
				_logger.Warning(ex, "Failed to set key '{key}' in distributed cache.", _key);
			}
			return toReturn;
		}

		public Task InvalidateAsync()
			=> _cache.RemoveAsync(_key);
	}
}
