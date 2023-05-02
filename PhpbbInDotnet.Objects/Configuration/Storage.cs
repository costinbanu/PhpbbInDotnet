using PhpbbInDotnet.Domain;

namespace PhpbbInDotnet.Objects.Configuration
{
    public class Storage
    {
        public string? Files { get; set; }

        public string? Avatars { get; set; }

        public string? Emojis { get; set; }

        public StorageType StorageType { get; set; }

        public string? ConnectionString { get; set; }
    }
}
