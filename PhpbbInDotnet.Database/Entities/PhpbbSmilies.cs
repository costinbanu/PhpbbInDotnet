using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PhpbbInDotnet.Database.Entities
{
    public partial class PhpbbSmilies
    {
        [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int SmileyId { get; set; } = 0;
        public string Code { get; set; } = string.Empty;
        public string Emotion { get; set; } = string.Empty;
        public string SmileyUrl { get; set; } = string.Empty;
        public short SmileyWidth { get; set; } = 0;
        public short SmileyHeight { get; set; } = 0;
        public int SmileyOrder { get; set; } = 0;
        public byte DisplayOnPosting { get; set; } = 0;
    }
}
