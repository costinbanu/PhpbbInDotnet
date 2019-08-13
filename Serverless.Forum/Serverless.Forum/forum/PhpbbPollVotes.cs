using System;
using System.Collections.Generic;

namespace Serverless.Forum.forum
{
    public partial class PhpbbPollVotes
    {
        public int TopicId { get; set; }
        public byte PollOptionId { get; set; }
        public int VoteUserId { get; set; }
        public string VoteUserIp { get; set; }
        public long Id { get; set; }
    }
}
