using System;
using System.Collections.Generic;

namespace PhpbbInDotnet.Forum.ForumDb.Entities
{
    public partial class PhpbbPrivmsgsFolder
    {
        public int FolderId { get; set; }
        public int UserId { get; set; }
        public string FolderName { get; set; }
        public int PmCount { get; set; }
    }
}
