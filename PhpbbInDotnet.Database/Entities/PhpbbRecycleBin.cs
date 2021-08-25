using PhpbbInDotnet.Utilities;
using System.ComponentModel.DataAnnotations.Schema;

namespace PhpbbInDotnet.Database.Entities
{
    public partial class PhpbbRecycleBin
    {
        [Column(TypeName = "int")]
        public RecycleBinItemType Type { get; set; }

        public int Id { get; set; }

        [Column(TypeName = "longblob")]
        public byte[] Content { get; set; }

        public long DeleteTime { get; set; }

        public int DeleteUser { get; set; }
    }
}
