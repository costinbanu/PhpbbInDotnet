using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PhpbbInDotnet.Database.Entities
{
    public partial class PhpbbBanlist
    {
        [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int BanId { get; set; } = 0;
        public int BanUserid { get; set; } = 0;
        public string BanIp { get; set; } = string.Empty;
        public string BanEmail { get; set; } = string.Empty;
        public int BanStart { get; set; } = 0;
        public int BanEnd { get; set; } = 0;
        public byte BanExclude { get; set; } = 0;
        public string BanReason { get; set; } = string.Empty;
        public string BanGiveReason { get; set; } = string.Empty;
    }
}
