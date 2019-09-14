using Serverless.Forum.Pages;
using System;
using System.Collections.Generic;

namespace Serverless.Forum.Contracts
{
    public class PostDisplay
    {
        public int? Id { get; set; } = null;

        public string PostTitle { get; set; } = null;

        public string PostText { get; set; } = null;

        public string AuthorName { get; set; } = null;

        public int? AuthorId { get; set; } = null;

        public DateTime? PostCreationTime { get; set; } = null;

        public DateTime? PostModifiedTime { get; set; } = null;

        public IEnumerable<_AttachmentPartialModel> Attachments { get; set; } = null;
    }
}
