using Serverless.Forum.Utilities;
using System;
using System.Collections.Generic;

namespace Serverless.Forum.Contracts
{
    public class Tracking
    {
        private string _postIds;

        public int TopicId { get; set; }

        public int ForumId { get; set; }

        public string PostIds
        {
            get => _postIds;
            set
            {
                _postIds = value;
                Posts = _postIds?.ToIntHashSet() ?? new HashSet<int>();
            }
        }

        public HashSet<int> Posts { get; private set; }

        public override bool Equals(object obj)
            => obj is Tracking tr && TopicId == tr.TopicId && PostIds.Equals(tr.PostIds, StringComparison.Ordinal);

        public override int GetHashCode()
            => HashCode.Combine(TopicId, PostIds);
    }
}
