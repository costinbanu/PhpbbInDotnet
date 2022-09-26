using System;

namespace PhpbbInDotnet.Objects
{
    public class Statistics
    {
        public DateTime? FirstMessageDate { get; set; }

        public int UserCount { get; set; }

        public int PostCount { get; set; }

        public int TopicCount { get; set; }

        public int ForumCount { get; set; }

        public int LatestUsersCount { get; set; }

        public int LatestPostsCount { get; set; }

        public long LatestFileSizeSum { get; set; }
    }
}
