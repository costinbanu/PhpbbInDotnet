using System;
using System.Collections.Generic;

namespace Serverless.Forum.Pages
{
    public class TopicDisplay
    {
        public int? Id { get; set; } = null;

        public string Title { get; set; } = null;

        public int? LastPosterId { get; set; } = null;

        public string LastPosterName { get; set; } = null;

        public DateTime? LastPostTime { get; set; } = null;

        public int? PostCount { get; set; } = null;

        public _PaginationPartialModel Pagination { get; set; } = null;

        public bool Unread { get; set; } = false;
    }

    public class TopicTransport
    {
        public byte? TopicType { get; set; } = null;

        public IEnumerable<TopicDisplay> Topics { get; set; }
    }
}