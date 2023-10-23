namespace PhpbbInDotnet.Objects.EmailDtos
{
	public class NewPostNotificationDto : SimpleEmailBody
	{
		public NewPostNotificationDto(string language, string userName, int postId, int topicId, string path) : base(language, userName)
		{
			PostId = postId;
			TopicId = topicId;
			Path = path;
		}

		public int PostId { get; }
		public int TopicId { get; }
		public string Path { get; }
	}
}
