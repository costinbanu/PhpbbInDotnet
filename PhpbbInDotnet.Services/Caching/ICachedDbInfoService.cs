using PhpbbInDotnet.Objects;
using System.Collections.Generic;

namespace PhpbbInDotnet.Services.Caching
{
	public interface ICachedDbInfoService
	{
		CacheItem<HashSet<ForumTopicCount>> ForumTopicCount { get; }
		CacheItem<HashSet<ForumTree>> ForumTree { get; }
	}
}