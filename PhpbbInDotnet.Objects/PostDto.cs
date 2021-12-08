using PhpbbInDotnet.Utilities;
using System;
using System.Collections.Generic;

namespace PhpbbInDotnet.Objects
{
    public class PostDto
    {
        public int ForumId { get; set; }

        public int TopicId { get; set; }

        public int PostId { get; set; }

        public string? PostSubject { get; set; }

        public string? PostText { get; set; }

        public string? AuthorName { get; set; }

        public int AuthorId { get; set; }

        public string? AuthorColor { get; set; }

        public string? BbcodeUid { get; set; }

        public DateTime? PostCreationTime => PostTime == 0 ? null : PostTime.ToUtcTime();

        public DateTime? PostModifiedTime => PostEditTime == 0 ? null : PostEditTime.ToUtcTime();

        public List<AttachmentDto>? Attachments { get; set; }

        public bool Unread { get; set; }

        public bool AuthorHasAvatar => !string.IsNullOrWhiteSpace(AuthorAvatar);

        public long PostEditTime { get; set; }

        public string? PostEditUser { get; set; }

        public short PostEditCount { get; set; }

        public string? PostEditReason { get; set; }

        public string? AuthorRank { get;  set; }

        public string? IP { get; set; }

        public ReportDto? Report { get; set; }

        public string? AuthorAvatar { get; set; }

        public long PostTime { get; set; }
    }
}
