﻿using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

namespace Serverless.Forum.ForumDb
{
    public partial class PhpbbPosts
    {
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int PostId { get; set; }
        public int TopicId { get; set; }
        public int ForumId { get; set; }
        public int PosterId { get; set; }
        public int IconId { get; set; }
        public string PosterIp { get; set; }
        public long PostTime { get; set; }
        public byte PostApproved { get; set; }
        public byte PostReported { get; set; }
        public byte EnableBbcode { get; set; }
        public byte EnableSmilies { get; set; }
        public byte EnableMagicUrl { get; set; }
        public byte EnableSig { get; set; }
        public string PostUsername { get; set; }
        public string PostSubject { get; set; }
        public string PostText { get; set; }
        public string PostChecksum { get; set; }
        public byte PostAttachment { get; set; }
        public string BbcodeBitfield { get; set; }
        public string BbcodeUid { get; set; }
        public byte PostPostcount { get; set; }
        public long PostEditTime { get; set; }
        public string PostEditReason { get; set; }
        public long PostEditUser { get; set; }
        public short PostEditCount { get; set; }
        public byte PostEditLocked { get; set; }
    }
}