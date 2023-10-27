using PhpbbInDotnet.Domain;

namespace PhpbbInDotnet.Objects.EmailDtos
{
    public class NewPostNotificationDto : SimpleEmailBody
	{
		public NewPostNotificationDto(string language, string userName, int postId, int topicId, string forumPath, string topicName) : base(language, userName)
		{
			PostId = postId;
			TopicId = topicId;
			Path = forumPath + Constants.FORUM_PATH_SEPARATOR + topicName;
			IsTopicNotification = true;
        }

        public NewPostNotificationDto(string language, string userName, int forumId, string forumPath) : base(language, userName)
        {
            ForumId = forumId;
            Path = forumPath;
			IsTopicNotification = false;
        }

		public bool IsTopicNotification { get; }
        public int? PostId { get; }
		public int? TopicId { get; }
		public int? ForumId { get; }
		public string Path { get; }
    }
}
