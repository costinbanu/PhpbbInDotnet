using PhpbbInDotnet.Utilities;
using PhpbbInDotnet.Utilities.Extensions;
using System;

namespace PhpbbInDotnet.Objects
{
    public class TopicDto
    {
        public int? DraftId { get; set; } = null;

        public int? TopicId { get; set; } = null;

        public int? ForumId { get; set; } = null;

        public string? TopicTitle { get; set; } = null;

        public int? TopicLastPosterId { get; set; } = null;

        public string? TopicLastPosterName { get; set; } = null;

        public DateTime? LastPostTime => TopicLastPostTime?.ToUtcTime();

        public TopicType? TopicType { get; set; } = null;

        public long? TopicLastPostTime { get; set; } = null;

        public int? TopicLastPostId { get; set; } = null;

        public int? PostCount { get; set; } = null;

        public PaginationDto? Pagination { get; set; } = null;

        public bool Unread { get; set; } = false;

        public string? TopicLastPosterColour { get; set; } = null;

        public int ViewCount { get; set; } = 0;

        public byte TopicStatus { get; set; }

        public bool IsLocked => TopicStatus == 1;

        public PollDto? Poll { get; set; }

    }
}