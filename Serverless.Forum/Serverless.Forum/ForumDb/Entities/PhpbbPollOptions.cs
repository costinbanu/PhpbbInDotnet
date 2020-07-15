﻿using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Serverless.Forum.ForumDb.Entities
{
    public partial class PhpbbPollOptions
    {
        public byte PollOptionId { get; set; }
        public int TopicId { get; set; }
        public string PollOptionText { get; set; }
        public int PollOptionTotal { get; set; }
        [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public ulong Id { get; set; } = 0;
    }
}