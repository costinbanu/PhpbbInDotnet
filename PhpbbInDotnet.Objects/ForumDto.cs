namespace PhpbbInDotnet.Objects
{
    public class ForumDto : UpsertForumDto
    {
        public int LeftId { get; set; } = 0;
        public int ForumLastPostId { get; set; } = 0;
        public int ForumLastPosterId { get; set; } = 0;
        public string ForumLastPostSubject { get; set; } = string.Empty;
        public long ForumLastPostTime { get; set; } = 0;
        public string ForumLastPosterName { get; set; } = string.Empty;
        public string ForumLastPosterColour { get; set; } = string.Empty;
        public int TotalCount { get; set; } = 0;
    }
}
