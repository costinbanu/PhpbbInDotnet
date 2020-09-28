﻿using Serverless.Forum.Pages.CustomPartials;
using Serverless.Forum.Utilities;
using System;
using System.Collections.Generic;

namespace Serverless.Forum.Pages
{
    public class TopicDto
    {
        public int? DraftId { get; set; } = null;

        public int? TopicId { get; set; } = null;

        public int? ForumId { get; set; } = null;

        public string TopicTitle { get; set; } = null;

        public int? TopicLastPosterId { get; set; } = null;

        public string TopicLastPosterName { get; set; } = null;

        public DateTime? LastPostTime => TopicLastPostTime?.ToUtcTime();

        public TopicType? TopicType { get; set; } = null;

        public long? TopicLastPostTime { get; set; } = null;

        public int? TopicLastPostId { get; set; } = null;

        public int? PostCount { get; set; } = null;

        public _PaginationPartialModel Pagination { get; set; } = null;

        public bool Unread { get; set; } = false;

        public string TopicLastPosterColour { get; set; } = null;

        public int ViewCount { get; set; } = 0;

        public byte TopicStatus { get; set; }

        public bool IsLocked => TopicStatus == 1;

    }

    public class TopicTransport
    {
        public TopicType? TopicType { get; set; } = null;

        public IEnumerable<TopicDto> Topics { get; set; }
    }
}