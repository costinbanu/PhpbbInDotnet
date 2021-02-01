using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PhpbbInDotnet.Database.Entities
{
    public partial class PhpbbBbcodes
    {
        [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public byte BbcodeId { get; set; } = 0;

        [DisplayFormat(ConvertEmptyStringToNull = false)]
        public string BbcodeTag { get; set; } = string.Empty;

        [DisplayFormat(ConvertEmptyStringToNull = false)]
        public string BbcodeHelpline { get; set; } = string.Empty;

        public byte DisplayOnPosting { get; set; } = 0;

        public string BbcodeMatch { get; set; } = string.Empty;

        [DisplayFormat(ConvertEmptyStringToNull = false)]
        public string BbcodeTpl { get; set; } = string.Empty;

        public string FirstPassMatch { get; set; } = string.Empty;

        public string FirstPassReplace { get; set; } = string.Empty;

        public string SecondPassMatch { get; set; } = string.Empty;

        public string SecondPassReplace { get; set; } = string.Empty;

    }
}
