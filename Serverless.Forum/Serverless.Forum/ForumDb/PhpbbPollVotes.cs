using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

namespace Serverless.Forum.ForumDb
{
    public partial class PhpbbPollVotes
    {
        public int TopicId { get; set; }
        public byte PollOptionId { get; set; }
        public int VoteUserId { get; set; }
        public string VoteUserIp { get; set; }
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public long Id { get; set; }
    }
}
