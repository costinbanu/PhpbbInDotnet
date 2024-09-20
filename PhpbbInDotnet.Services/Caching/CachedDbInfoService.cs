using LazyCache;
using PhpbbInDotnet.Database.SqlExecuter;
using PhpbbInDotnet.Objects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PhpbbInDotnet.Services.Caching
{
	class CachedDbInfoService : ICachedDbInfoService
	{
		private readonly ISqlExecuter _sqlExecuter;

		public CacheItem<HashSet<ForumTopicCount>> ForumTopicCount { get; }
		public CacheItem<HashSet<ForumTree>> ForumTree { get; }

		public CachedDbInfoService(ISqlExecuter sqlExecuter, IAppCache appCache)
		{
			_sqlExecuter = sqlExecuter;

			ForumTopicCount = new(
				factory: async () =>
				{
					var count = await _sqlExecuter.QueryAsync<ForumTopicCount>(
						"SELECT forum_id, count(topic_id) as topic_count FROM phpbb_topics GROUP BY forum_id");
					return count.ToHashSet();
				},
				key: nameof(ForumTopicCount),
				cache: appCache);

			ForumTree = new(
				factory: async () =>
				{
					var tree = await _sqlExecuter.CallStoredProcedureAsync<ForumTree>("get_forum_tree");
					return tree.ToHashSet();
				},
				key: nameof(ForumTree),
				cache: appCache);

		}

	}
}
