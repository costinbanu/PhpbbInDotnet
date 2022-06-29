using PhpbbInDotnet.Domain;

namespace PhpbbInDotnet.Objects
{
    public class DeletedItemDto
    {
        public RecycleBinItemType Type { get; set; }

        public int Id { get; set; }

        public byte[]? RawContent { get; set; }

        public object? Value { get; set; }

        public long DeleteTime { get; set; }

        public int DeleteUser { get; set; }

        public string? DeleteUserName { get; set; }
    }
}
