﻿using System;

namespace PhpbbInDotnet.Objects
{
    public class ForumTopicCount
    {
        public int ForumId { get; set; }

        public int TopicCount { get; set; }

        public override bool Equals(object? obj)
            => obj is ForumTopicCount topicList && ForumId == topicList?.ForumId;

        public override int GetHashCode()
            => HashCode.Combine(ForumId);
    }
}
