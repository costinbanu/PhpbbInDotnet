using System;
using System.Collections.Generic;

namespace Serverless.Forum.ForumDb
{
    public partial class PhpbbPrivmsgsFolder
    {
        public int FolderId { get; set; }
        public int UserId { get; set; }
        public string FolderName { get; set; }
        public int PmCount { get; set; }
    }
}
