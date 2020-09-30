using System;
using System.Collections.Generic;

namespace PhpbbInDotnet.Forum.ForumDb.Entities
{
    public partial class PhpbbUserGroup
    {
        public int GroupId { get; set; }
        public int UserId { get; set; }
        public byte GroupLeader { get; set; }
        public byte UserPending { get; set; }
    }
}
