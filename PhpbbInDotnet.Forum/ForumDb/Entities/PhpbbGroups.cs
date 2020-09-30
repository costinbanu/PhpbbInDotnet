using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PhpbbInDotnet.Forum.ForumDb.Entities
{
    public partial class PhpbbGroups
    {
        [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int GroupId { get; set; } = 0;
        public byte GroupType { get; set; } = 1;
        public byte GroupFounderManage { get; set; } = 0;
        public byte GroupSkipAuth { get; set; } = 0;
        public string GroupName { get; set; } = string.Empty;
        public string GroupDesc { get; set; } = string.Empty;
        public string GroupDescBitfield { get; set; } = string.Empty;
        public int GroupDescOptions { get; set; } = 7;
        public string GroupDescUid { get; set; } = string.Empty;
        public byte GroupDisplay { get; set; } = 0;
        public string GroupAvatar { get; set; } = string.Empty;
        public byte GroupAvatarType { get; set; } = 0;
        public short GroupAvatarWidth { get; set; } = 0;
        public short GroupAvatarHeight { get; set; } = 0;
        public int GroupRank { get; set; } = 0;
        public string GroupColour { get; set; } = string.Empty;
        public int GroupSigChars { get; set; } = 0;
        public byte GroupReceivePm { get; set; } = 0;
        public int GroupMessageLimit { get; set; } = 0;
        public int GroupMaxRecipients { get; set; } = 0;
        public byte GroupLegend { get; set; } = 1;
        public int GroupUserUploadSize { get; set; } = 0;
        public int GroupEditTime { get; set; } = 60;
    }
}
