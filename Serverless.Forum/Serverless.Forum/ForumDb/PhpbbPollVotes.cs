using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Serverless.Forum.ForumDb
{
    public partial class PhpbbPollVotes
    {
        public int TopicId { get; set; }
        public byte PollOptionId { get; set; }
        public int VoteUserId { get; set; }
        public string VoteUserIp { get; set; }
        [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public ulong Id { get; set; } = 0;
    }
}
