using System;
using System.Collections.Generic;

namespace PhpbbInDotnet.Forum.ForumDb.Entities
{
    public partial class PhpbbForumsWatch
    {
        public int ForumId { get; set; }
        public int UserId { get; set; }
        public byte NotifyStatus { get; set; }
    }
}
