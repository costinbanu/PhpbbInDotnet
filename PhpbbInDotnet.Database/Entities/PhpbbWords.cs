using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PhpbbInDotnet.Database.Entities
{
    public partial class PhpbbWords
    {
        [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int WordId { get; set; }

        [DisplayFormat(ConvertEmptyStringToNull = false)]
        public string Word { get; set; } = string.Empty;

        [DisplayFormat(ConvertEmptyStringToNull = false)]
        public string Replacement { get; set; } = string.Empty;
    }
}
