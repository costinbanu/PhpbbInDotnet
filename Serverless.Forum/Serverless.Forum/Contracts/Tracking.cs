using Serverless.Forum.Utilities;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

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
            => obj != null && obj is Tracking tr && TopicId == tr.TopicId;

        public override int GetHashCode()
            => TopicId.GetHashCode();
    }

    public class TrackingComparerByForumId : IEqualityComparer<Tracking>
    {
        public bool Equals([AllowNull] Tracking x, [AllowNull] Tracking y)
            => x != null && y != null && x.ForumId == y.ForumId;

        public int GetHashCode([DisallowNull] Tracking obj)
            => obj.ForumId.GetHashCode();
    }
}
