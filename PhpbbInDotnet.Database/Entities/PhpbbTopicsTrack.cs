namespace PhpbbInDotnet.Database.Entities
{
    public partial class PhpbbTopicsTrack
    {
        public int UserId { get; set; }
        public int TopicId { get; set; }
        public int ForumId { get; set; }
        public long MarkTime { get; set; }
    }
}
