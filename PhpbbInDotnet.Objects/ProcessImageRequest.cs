namespace PhpbbInDotnet.Objects
{
    public class ProcessImageRequest
    {
        public long? SizeLimit { get; set; }

        public bool HideLicensePlates { get; set; }

        public byte[]? Contents { get; set; }

        public string? FileName { get; set; }

        public string? MimeType { get; set; }
    }
}
