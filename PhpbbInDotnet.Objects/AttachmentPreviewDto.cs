namespace PhpbbInDotnet.Objects
{
    public class AttachmentPreviewDto
    {
        public int Id { get; set; }
        public string? PhysicalFilename { get; set; }
        public string? RealFilename { get; set; }
        public string? Mimetype { get; set; }
        public long FileSize { get; set; }
        public long FileTime { get; set; }
        public int? ForumId { get; set; }
        public int? PostId { get; set; }
        public string? TopicTitle { get; set; }
    }
}
