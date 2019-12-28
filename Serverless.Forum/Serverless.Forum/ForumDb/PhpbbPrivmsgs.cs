using System;
using System.Collections.Generic;

namespace Serverless.Forum.ForumDb
{
    public partial class PhpbbPrivmsgs
    {
        public int MsgId { get; set; }
        public int RootLevel { get; set; }
        public int AuthorId { get; set; }
        public int IconId { get; set; }
        public string AuthorIp { get; set; }
        public int MessageTime { get; set; }
        public byte EnableBbcode { get; set; }
        public byte EnableSmilies { get; set; }
        public byte EnableMagicUrl { get; set; }
        public byte EnableSig { get; set; }
        public string MessageSubject { get; set; }
        public string MessageText { get; set; }
        public string MessageEditReason { get; set; }
        public int MessageEditUser { get; set; }
        public byte MessageAttachment { get; set; }
        public string BbcodeBitfield { get; set; }
        public string BbcodeUid { get; set; }
        public int MessageEditTime { get; set; }
        public short MessageEditCount { get; set; }
        public string ToAddress { get; set; }
        public string BccAddress { get; set; }
        public byte MessageReported { get; set; }
    }
}
