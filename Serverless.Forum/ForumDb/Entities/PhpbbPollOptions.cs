namespace Serverless.Forum.ForumDb.Entities
{
    public partial class PhpbbPollOptions
    {
        public byte PollOptionId { get; set; }
        public int TopicId { get; set; }
        public string PollOptionText { get; set; }
        public int PollOptionTotal { get; set; }

    }
}
