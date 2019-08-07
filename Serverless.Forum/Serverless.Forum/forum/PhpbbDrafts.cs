using System;
using System.Collections.Generic;

namespace Serverless.Forum.forum
{
    public partial class PhpbbDrafts
    {
        public int DraftId { get; set; }
        public int UserId { get; set; }
        public int TopicId { get; set; }
        public int ForumId { get; set; }
        public int SaveTime { get; set; }
        public string DraftSubject { get; set; }
        public string DraftMessage { get; set; }
    }
}
