using Serverless.Forum.Pages.CustomPartials;
using System;
using System.Collections.Generic;

namespace Serverless.Forum.Contracts
{
    public class PostDisplay
    {
        public int? PostId { get; set; } = null;

        public string PostSubject { get; set; } = null;

        public string PostText { get; set; } = null;

        public string AuthorName { get; set; } = null;

        public int? AuthorId { get; set; } = null;

        public string AuthorColor { get; set; } = null;

        public string BbcodeUid { get; set; } = null;

        public DateTime? PostCreationTime { get; set; } = null;

        public DateTime? PostModifiedTime { get; set; } = null;

        public List<_AttachmentPartialModel> Attachments { get; set; } = null;

        public bool Unread { get; set; }

        public bool AuthorHasAvatar { get; set; } = false;

        public string AuthorSignature { get; set; }
    }
}
