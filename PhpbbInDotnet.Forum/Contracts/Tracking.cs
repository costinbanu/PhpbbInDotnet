using PhpbbInDotnet.Forum.Utilities;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace PhpbbInDotnet.Forum.Contracts
{
    public class Tracking
    {
        public int TopicId { get; set; }

        public HashSet<int> Posts { get; set; }

        public override bool Equals(object obj)
            => obj != null && obj is Tracking tr && TopicId == tr.TopicId;

        public override int GetHashCode()
            => TopicId.GetHashCode();
    }

    public class ExtendedTracking : Tracking
    {
        public string PostIds { get; set; }
        public int ForumId { get; set; }
    }
}
