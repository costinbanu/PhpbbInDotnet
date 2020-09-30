namespace PhpbbInDotnet.Database.Entities
{
    public partial class PhpbbTopicsPosted
    {
        public int UserId { get; set; }
        public int TopicId { get; set; }
        public byte TopicPosted { get; set; }
    }
}
