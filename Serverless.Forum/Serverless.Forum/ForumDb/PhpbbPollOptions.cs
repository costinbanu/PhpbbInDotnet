using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

namespace Serverless.Forum.ForumDb
{
    public partial class PhpbbPollOptions
    {
        public byte PollOptionId { get; set; }
        public int TopicId { get; set; }
        public string PollOptionText { get; set; }
        public int PollOptionTotal { get; set; }
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public long Id { get; set; }
    }
}
