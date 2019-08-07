using System;
using System.Collections.Generic;

namespace Serverless.Forum.forum
{
    public partial class PhpbbGroups
    {
        public int GroupId { get; set; }
        public byte GroupType { get; set; }
        public byte GroupFounderManage { get; set; }
        public byte GroupSkipAuth { get; set; }
        public string GroupName { get; set; }
        public string GroupDesc { get; set; }
        public string GroupDescBitfield { get; set; }
        public int GroupDescOptions { get; set; }
        public string GroupDescUid { get; set; }
        public byte GroupDisplay { get; set; }
        public string GroupAvatar { get; set; }
        public byte GroupAvatarType { get; set; }
        public short GroupAvatarWidth { get; set; }
        public short GroupAvatarHeight { get; set; }
        public int GroupRank { get; set; }
        public string GroupColour { get; set; }
        public int GroupSigChars { get; set; }
        public byte GroupReceivePm { get; set; }
        public int GroupMessageLimit { get; set; }
        public int GroupMaxRecipients { get; set; }
        public byte GroupLegend { get; set; }
        public int GroupUserUploadSize { get; set; }
    }
}
