namespace PhpbbInDotnet.Database.Entities
{
    public partial class PhpbbPollOptions
    {
        public byte PollOptionId { get; set; }
        public int TopicId { get; set; }
        public string PollOptionText { get; set; } = string.Empty;
        public int PollOptionTotal { get; set; }

    }
}
