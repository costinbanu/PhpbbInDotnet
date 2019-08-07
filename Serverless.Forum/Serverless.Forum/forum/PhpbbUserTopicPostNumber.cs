using System;
using System.Collections.Generic;

namespace Serverless.Forum.forum
{
    public partial class PhpbbUserTopicPostNumber
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public int TopicId { get; set; }
        public int PostNo { get; set; }
    }
}
