using System;
using System.Collections.Generic;

namespace Serverless.Forum.ForumDb.Entities
{
    public partial class PhpbbForumsTrack
    {
        public int UserId { get; set; }
        public int ForumId { get; set; }
        public long MarkTime { get; set; }
    }
}
