using PhpbbInDotnet.Utilities;

namespace PhpbbInDotnet.Objects
{
    public class DeletedItemDto
    {
        public RecycleBinItemType Type { get; set; }

        public int Id { get; set; }

        public byte[] Content { get; set; }

        public long DeleteTime { get; set; }

        public int DeleteUser { get; set; }

        public string DeleteUserName { get; set; }
    }
}
