using System;
using System.Collections.Generic;

namespace Serverless.Forum.forum
{
    public partial class PhpbbPrivmsgsTo
    {
        public int MsgId { get; set; }
        public int UserId { get; set; }
        public int AuthorId { get; set; }
        public byte PmDeleted { get; set; }
        public byte PmNew { get; set; }
        public byte PmUnread { get; set; }
        public byte PmReplied { get; set; }
        public byte PmMarked { get; set; }
        public byte PmForwarded { get; set; }
        public int FolderId { get; set; }
        public long Id { get; set; }
    }
}
