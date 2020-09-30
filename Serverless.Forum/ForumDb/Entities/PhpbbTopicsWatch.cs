using System;
using System.Collections.Generic;

namespace Serverless.Forum.ForumDb.Entities
{
    public partial class PhpbbTopicsWatch
    {
        public int TopicId { get; set; }
        public int UserId { get; set; }
        public byte NotifyStatus { get; set; }
    }
}
