using System;
using System.Collections.Generic;

namespace PhpbbInDotnet.Forum.ForumDb.Entities
{
    public partial class PhpbbModeratorCache
    {
        public int ForumId { get; set; }
        public int UserId { get; set; }
        public string Username { get; set; }
        public int GroupId { get; set; }
        public string GroupName { get; set; }
        public byte DisplayOnIndex { get; set; }
    }
}
