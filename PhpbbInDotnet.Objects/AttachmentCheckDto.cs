namespace PhpbbInDotnet.Objects
{
    public class AttachmentCheckDto
    {
        public string PhysicalFilename { get; set; }
        public string RealFilename { get; set; }
        public string Mimetype { get; set; }
        public int? ForumId { get; set; }
        public int? PostId { get; set; }
    }
}
