namespace PhpbbInDotnet.Objects.Configuration
{
    public class ExternalImageProcessor
    {
        public bool Enabled { get; set; }

        public string Name { get; set; }

        public string Url { get; set; }

        public ExternalImageProcessorApi Api { get; set; }
    }
}
