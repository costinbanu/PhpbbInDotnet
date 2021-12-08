using System.Collections.Generic;

namespace PhpbbInDotnet.Objects
{
    public class Tracking
    {
        public int TopicId { get; set; }

        public HashSet<int>? Posts { get; set; }

        public override bool Equals(object? obj)
            => obj is Tracking tr && TopicId == tr.TopicId;

        public override int GetHashCode()
            => TopicId.GetHashCode();
    }

    public class ExtendedTracking : Tracking
    {
        public string? PostIds { get; set; }
        public int ForumId { get; set; }
    }
}
