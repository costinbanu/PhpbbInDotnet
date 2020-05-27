using Serverless.Forum.Pages.CustomPartials;
using Serverless.Forum.Utilities;
using System;
using System.Collections.Generic;

namespace Serverless.Forum.Pages
{
    public class TopicDto
    {
        public int? Id { get; set; } = null;

        public string Title { get; set; } = null;

        public int? LastPosterId { get; set; } = null;

        public string LastPosterName { get; set; } = null;

        public DateTime? LastPostTime { get; set; } = null;

        public int? LastPostId { get; set; } = null;

        public int? PostCount { get; set; } = null;

        public _PaginationPartialModel Pagination { get; set; } = null;

        public bool Unread { get; set; } = false;

        public string LastPosterColor { get; set; } = null;

        public int ViewCount { get; set; } = 0;
    }

    public class TopicTransport
    {
        public TopicType? TopicType { get; set; } = null;

        public IEnumerable<TopicDto> Topics { get; set; }
    }
}