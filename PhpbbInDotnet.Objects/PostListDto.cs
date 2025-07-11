using System.Collections.Generic;

namespace PhpbbInDotnet.Objects
{
    public class PostListDto
    {
        public List<PostDto> Posts { get; set; } = new List<PostDto> { new PostDto() };
        public Dictionary<int, List<AttachmentDto>> Attachments { get; set; } = new Dictionary<int, List<AttachmentDto>> { };
        public int? PostCount { get; set; }
        public List<ReportDto> Reports { get; set; } = new List<ReportDto> { };
    }
}
