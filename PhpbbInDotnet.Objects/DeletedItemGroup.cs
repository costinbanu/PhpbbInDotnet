using PhpbbInDotnet.Utilities;
using System.Collections.Generic;

namespace PhpbbInDotnet.Objects
{
    public class DeletedItemGroup
    {
        public RecycleBinItemType Type { get; set; }

        public IEnumerable<DeletedItemDto>? Items { get; set; }
    }
}
