using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Serverless.Forum.ForumDb
{
    public partial class PhpbbPosts
    {
        [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int PostId { get; set; } = 0;
        public int TopicId { get; set; } = 0;
        public int ForumId { get; set; } = 0;
        public int PosterId { get; set; } = 0;
        public int IconId { get; set; } = 0;
        public string PosterIp { get; set; } = string.Empty;
        public long PostTime { get; set; } = 0;
        public byte PostApproved { get; set; } = 1;
        public byte PostReported { get; set; } = 0;
        public byte EnableBbcode { get; set; } = 1;
        public byte EnableSmilies { get; set; } = 1;
        public byte EnableMagicUrl { get; set; } = 1;
        public byte EnableSig { get; set; } = 1;
        public string PostUsername { get; set; } = string.Empty;
        public string PostSubject { get; set; } = string.Empty;
        public string PostText { get; set; } = string.Empty;
        public string PostChecksum { get; set; } = string.Empty;
        public byte PostAttachment { get; set; } = 0;
        public string BbcodeBitfield { get; set; } = string.Empty;
        public string BbcodeUid { get; set; } = string.Empty;
        public byte PostPostcount { get; set; } = 0;
        public long PostEditTime { get; set; } = 0;
        public string PostEditReason { get; set; } = string.Empty;
        public int PostEditUser { get; set; } = 0;
        public short PostEditCount { get; set; } = 0;
        public byte PostEditLocked { get; set; } = 0;
    }
}
