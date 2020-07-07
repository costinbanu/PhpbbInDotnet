using System;
using System.Collections.Generic;

namespace Serverless.Forum.ForumDb.Entities
{
    public partial class PhpbbForumsAccess
    {
        public int ForumId { get; set; }
        public int UserId { get; set; }
        public string SessionId { get; set; }
    }
}
