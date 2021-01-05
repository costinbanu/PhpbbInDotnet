using System;

namespace PhpbbInDotnet.Objects
{
    public class Statistics
    {
        public DateTime? FirstMessageDate { get; set; }

        public int UserCount { get; set; }

        public int MessageCount { get; set; }

        public int TopicCount { get; set; }

        public int ForumCount { get; set; }
    }
}
