﻿using Serverless.Forum.Utilities;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

namespace Serverless.Forum.ForumDb
{
    public partial class PhpbbUsers
    {
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int UserId { get; set; }
        public byte UserType { get; set; }
        public int GroupId { get; set; }
        public string UserPermissions { get; set; }
        public int UserPermFrom { get; set; }
        public string UserIp { get; set; }
        public int UserRegdate { get; set; }
        public string Username { get; set; }
        public string UsernameClean { get; set; }
        public string UserPassword { get; set; }
        public int UserPasschg { get; set; }
        public byte UserPassConvert { get; set; }
        public string UserEmail { get; set; }
        public long UserEmailHash { get; set; }
        public string UserBirthday { get; set; }
        public int UserLastvisit { get; set; }
        public int UserLastmark { get; set; }
        public long UserLastpostTime { get; set; }
        public string UserLastpage { get; set; }
        public string UserLastConfirmKey { get; set; }
        public int UserLastSearch { get; set; }
        public byte UserWarnings { get; set; }
        public int UserLastWarning { get; set; }
        public byte UserLoginAttempts { get; set; }
        [Column(TypeName = "tinyint(2)")]
        public UserInactiveReason UserInactiveReason { get; set; }
        public long UserInactiveTime { get; set; }
        public int UserPosts { get; set; }
        public string UserLang { get; set; }
        public decimal UserTimezone { get; set; }
        public byte UserDst { get; set; }
        public string UserDateformat { get; set; }
        public int UserStyle { get; set; }
        public int UserRank { get; set; }
        public string UserColour { get; set; }
        public int UserNewPrivmsg { get; set; }
        public int UserUnreadPrivmsg { get; set; }
        public int UserLastPrivmsg { get; set; }
        public byte UserMessageRules { get; set; }
        public int UserFullFolder { get; set; }
        public long UserEmailtime { get; set; }
        public short UserTopicShowDays { get; set; }
        public string UserTopicSortbyType { get; set; }
        public string UserTopicSortbyDir { get; set; }
        public short UserPostShowDays { get; set; }
        public string UserPostSortbyType { get; set; }
        public string UserPostSortbyDir { get; set; }
        public byte UserNotify { get; set; }
        public byte UserNotifyPm { get; set; }
        public byte UserNotifyType { get; set; }
        public byte UserAllowPm { get; set; }
        public byte UserAllowViewonline { get; set; }
        public byte UserAllowViewemail { get; set; }
        public byte UserAllowMassemail { get; set; }
        public int UserOptions { get; set; }
        public string UserAvatar { get; set; }
        public byte UserAvatarType { get; set; }
        public short UserAvatarWidth { get; set; }
        public short UserAvatarHeight { get; set; }
        public string UserSig { get; set; }
        public string UserSigBbcodeUid { get; set; }
        public string UserSigBbcodeBitfield { get; set; }
        public string UserFrom { get; set; }
        public string UserIcq { get; set; }
        public string UserAim { get; set; }
        public string UserYim { get; set; }
        public string UserMsnm { get; set; }
        public string UserJabber { get; set; }
        public string UserWebsite { get; set; }
        public string UserOcc { get; set; }
        public string UserInterests { get; set; }
        public string UserActkey { get; set; }
        public string UserNewpasswd { get; set; }
        public string UserFormSalt { get; set; }
        public byte UserNew { get; set; }
        public byte UserReminded { get; set; }
        public long UserRemindedTime { get; set; }
        public byte? AcceptedNewTerms { get; set; }
        public long UserEditTime { get; set; }
    }
}
