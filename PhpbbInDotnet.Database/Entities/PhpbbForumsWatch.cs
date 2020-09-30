namespace PhpbbInDotnet.Database.Entities
{
    public partial class PhpbbForumsWatch
    {
        public int ForumId { get; set; }
        public int UserId { get; set; }
        public byte NotifyStatus { get; set; }
    }
}
