using System;
using System.Collections.Generic;

namespace Serverless.Forum.ForumDb
{
    public partial class PhpbbTopicsPosted
    {
        public int UserId { get; set; }
        public int TopicId { get; set; }
        public byte TopicPosted { get; set; }
    }
}
