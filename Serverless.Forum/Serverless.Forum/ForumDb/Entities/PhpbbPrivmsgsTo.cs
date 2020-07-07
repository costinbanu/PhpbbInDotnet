using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Serverless.Forum.ForumDb.Entities
{
    public partial class PhpbbPrivmsgsTo
    {
        public int MsgId { get; set; } = 0;
        public int UserId { get; set; } = 0;
        public int AuthorId { get; set; } = 0;
        public byte PmDeleted { get; set; } = 0;
        public byte PmNew { get; set; } = 1;
        public byte PmUnread { get; set; } = 1;
        public byte PmReplied { get; set; } = 0;
        public byte PmMarked { get; set; } = 0;
        public byte PmForwarded { get; set; } = 0;
        public int FolderId { get; set; } = 0;
        [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public long Id { get; set; } = 0;
    }
}
