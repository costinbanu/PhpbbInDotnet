using System;
using System.Collections.Generic;

namespace PhpbbInDotnet.Forum.ForumDb.Entities
{
    public partial class PhpbbAclOptions
    {
        public int AuthOptionId { get; set; }
        public string AuthOption { get; set; }
        public byte IsGlobal { get; set; }
        public byte IsLocal { get; set; }
        public byte FounderOnly { get; set; }
    }
}
