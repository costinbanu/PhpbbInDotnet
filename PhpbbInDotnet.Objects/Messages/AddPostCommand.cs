namespace PhpbbInDotnet.Objects.Messages
{
    public class AddPostCommand : IBackgroundMessage
    {
        public string QueueName => "add-post";
        public string? PostText { get; set; }
    }
}
