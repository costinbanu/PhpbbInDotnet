using PhpbbInDotnet.Database.Entities;
using PhpbbInDotnet.Utilities;
using System.Collections.Generic;

namespace PhpbbInDotnet.Objects.Deleted
{
    public class DeletedItemDto
    {
        public RecycleBinItemType Type { get; set; }

        public IEnumerable<PhpbbRecycleBin> Items { get; set; }
    }
}
