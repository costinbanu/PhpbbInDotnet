﻿using System;
using System.Collections.Generic;

namespace Serverless.Forum.forum
{
    public partial class PhpbbPollOptions
    {
        public byte PollOptionId { get; set; }
        public int TopicId { get; set; }
        public string PollOptionText { get; set; }
        public int PollOptionTotal { get; set; }
        public long Id { get; set; }
    }
}
