using PhpbbInDotnet.Utilities;
using System;
using System.Collections.Generic;

namespace PhpbbInDotnet.Objects
{
    public class PostDto
    {
        public int ForumId { get; set; }

        public int TopicId { get; set; }

        public int? PostId { get; set; } = null;

        public string PostSubject { get; set; } = null;

        public string PostText { get; set; } = null;

        public string AuthorName { get; set; } = null;

        public int? AuthorId { get; set; } = null;

        public string AuthorColor { get; set; } = null;

        public string BbcodeUid { get; set; } = null;

        public DateTime? PostCreationTime => PostTime == 0 ? null : PostTime.ToUtcTime();

        public DateTime? PostModifiedTime => LastEditTime == 0 ? null : LastEditTime.ToUtcTime();

        public List<AttachmentDto> Attachments { get; set; } = null;

        public bool Unread { get; set; }

        public bool AuthorHasAvatar => !string.IsNullOrWhiteSpace(AuthorAvatar);

        public long LastEditTime { get; set; } = 0;

        public string LastEditUser { get; set; }

        public short EditCount { get; set; } = 0;

        public string LastEditReason { get; set; }

        public string AuthorRank { get;  set; }

        public string IP { get; set; }

        public ReportDto Report { get; set; }

        public string AuthorAvatar { get; set; }

        public long PostTime { get; set; }
    }
}
