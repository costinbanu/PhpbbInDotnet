using System;
using System.Collections.Generic;

namespace PhpbbInDotnet.Forum.ForumDb.Entities
{
    public partial class PhpbbSessionsKeys
    {
        public string KeyId { get; set; }
        public int UserId { get; set; }
        public string LastIp { get; set; }
        public int LastLogin { get; set; }
    }
}
