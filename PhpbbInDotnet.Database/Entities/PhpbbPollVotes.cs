namespace PhpbbInDotnet.Database.Entities
{
    public partial class PhpbbPollVotes
    {
        public int TopicId { get; set; }
        public byte PollOptionId { get; set; }
        public int VoteUserId { get; set; }
        public string VoteUserIp { get; set; }

    }
}
