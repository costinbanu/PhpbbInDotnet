using PhpbbInDotnet.Database;

namespace PhpbbInDotnet.Objects
{
    public class AttachmentPreviewDto : PaginatedResultSet
    {
        public int AttachId { get; set; }
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
