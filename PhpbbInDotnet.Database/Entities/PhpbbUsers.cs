using PhpbbInDotnet.Utilities;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PhpbbInDotnet.Database.Entities
{
    public partial class PhpbbUsers
    {
        [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int UserId { get; set; } = 0;
        public byte UserType { get; set; } = 0;
        public int GroupId { get; set; } = 3;
        public string UserPermissions { get; set; } = string.Empty;
        public int UserPermFrom { get; set; } = 0;
        public string UserIp { get; set; } = string.Empty;
        public long UserRegdate { get; set; } = 0;
        public string Username { get; set; } = string.Empty;
        public string UsernameClean { get; set; } = string.Empty;
        public string UserPassword { get; set; } = string.Empty;
        public long UserPasschg { get; set; } = 0;
        public byte UserPassConvert { get; set; } = 0;
        public string UserEmail { get; set; } = string.Empty;
        public long UserEmailHash { get; set; } = 0;
        public string UserBirthday { get; set; } = string.Empty;
        public long UserLastvisit { get; set; } = 0;
        public long UserLastmark { get; set; } = 0;
        public long UserLastpostTime { get; set; } = 0;
        public string UserLastpage { get; set; } = string.Empty;
        public string UserLastConfirmKey { get; set; } = string.Empty;
        public long UserLastSearch { get; set; } = 0;
        public byte UserWarnings { get; set; } = 0;
        public long UserLastWarning { get; set; } = 0;
        public byte UserLoginAttempts { get; set; } = 0;
        [Column(TypeName = "tinyint(2)")]
        public UserInactiveReason UserInactiveReason { get; set; } = UserInactiveReason.NotInactive;
        public long UserInactiveTime { get; set; } = 0;
        public int UserPosts { get; set; } = 0;
        public string UserLang { get; set; } = string.Empty;
        public decimal UserTimezone { get; set; } = 0m;
        public byte UserDst { get; set; } = 0;
        public string UserDateformat { get; set; } = "dd.MM.yyyy HH.mm";
        public int UserStyle { get; set; } = 0;
        public int UserRank { get; set; } = 0;
        public string UserColour { get; set; } = string.Empty;
        public int UserNewPrivmsg { get; set; } = 0;
        public int UserUnreadPrivmsg { get; set; } = 0;
        public long UserLastPrivmsg { get; set; } = 0;
        public byte UserMessageRules { get; set; } = 0;
        public int UserFullFolder { get; set; } = -3;
        public long UserEmailtime { get; set; } = 0;
        public short UserTopicShowDays { get; set; } = 0;
        public string UserTopicSortbyType { get; set; } = "t";
        public string UserTopicSortbyDir { get; set; } = "d";
        public short UserPostShowDays { get; set; } = 0;
        public string UserPostSortbyType { get; set; } = "t";
        public string UserPostSortbyDir { get; set; } = "a";
        public byte UserNotify { get; set; } = 0;
        public byte UserNotifyPm { get; set; } = 1;
        public byte UserNotifyType { get; set; } = 0;
        public byte UserAllowPm { get; set; } = 1;
        public byte UserAllowViewonline { get; set; } = 1;
        public byte UserAllowViewemail { get; set; } = 1;
        public byte UserAllowMassemail { get; set; } = 0;
        public int UserOptions { get; set; } = 230271;
        public string UserAvatar { get; set; } = string.Empty;
        public byte UserAvatarType { get; set; } = 0;
        public short UserAvatarWidth { get; set; } = 0;
        public short UserAvatarHeight { get; set; } = 0;
        public string UserSig { get; set; } = string.Empty;
        public string UserSigBbcodeUid { get; set; } = string.Empty;
        public string UserSigBbcodeBitfield { get; set; } = string.Empty;
        public string UserFrom { get; set; } = string.Empty;
        public string UserIcq { get; set; } = string.Empty;
        public string UserAim { get; set; } = string.Empty;
        public string UserYim { get; set; } = string.Empty;
        public string UserMsnm { get; set; } = string.Empty;
        public string UserJabber { get; set; } = string.Empty;
        public string UserWebsite { get; set; } = string.Empty;
        public string UserOcc { get; set; } = string.Empty;
        public string UserInterests { get; set; } = string.Empty;
        public string UserActkey { get; set; } = string.Empty;
        public string UserNewpasswd { get; set; } = string.Empty;
        public string UserFormSalt { get; set; } = string.Empty;
        public byte UserNew { get; set; } = 1;
        public byte UserReminded { get; set; } = 0;
        public long UserRemindedTime { get; set; } = 0;
        public byte? AcceptedNewTerms { get; set; } = null;
        public int UserEditTime { get; set; } = 60;
    }
}
