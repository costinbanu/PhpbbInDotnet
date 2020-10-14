using System;

namespace PhpbbInDotnet.DTOs
{
    public class ForumTopicCount
    {
        public int ForumId { get; set; }

        public int TopicCount { get; set; }

        public override bool Equals(object obj)
            => obj != null && obj is ForumTopicCount topicList && ForumId == topicList?.ForumId;

        public override int GetHashCode()
            => HashCode.Combine(ForumId);
    }
}
