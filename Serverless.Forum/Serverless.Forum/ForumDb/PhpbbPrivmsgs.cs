using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Serverless.Forum.ForumDb
{
    public partial class PhpbbPrivmsgs
    {
        [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int MsgId { get; set; } = 0;
        public int RootLevel { get; set; } = 0;
        public int AuthorId { get; set; } = 0;
        public int IconId { get; set; } = 0;
        public string AuthorIp { get; set; } = string.Empty;
        public long MessageTime { get; set; } = 0;
        public byte EnableBbcode { get; set; } = 1;
        public byte EnableSmilies { get; set; } = 1;
        public byte EnableMagicUrl { get; set; } = 1;
        public byte EnableSig { get; set; } = 1;
        public string MessageSubject { get; set; } = string.Empty;
        public string MessageText { get; set; } = string.Empty;
        public string MessageEditReason { get; set; } = string.Empty;
        public int MessageEditUser { get; set; } = 0;
        public byte MessageAttachment { get; set; } = 0;
        public string BbcodeBitfield { get; set; } = string.Empty;
        public string BbcodeUid { get; set; } = string.Empty;
        public int MessageEditTime { get; set; } = 0;
        public short MessageEditCount { get; set; } = 0;
        public string ToAddress { get; set; } = string.Empty;
        public string BccAddress { get; set; } = string.Empty;
        public byte MessageReported { get; set; } = 0;
    }
}
