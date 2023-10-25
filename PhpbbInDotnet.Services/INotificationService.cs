using System.Threading.Tasks;

namespace PhpbbInDotnet.Services
{
	public interface INotificationService
	{
		Task SendNewPostNotification(int authorId, int forumId, int topicId, int postId, string forumPath, string topicTitle);
		Task ToggleTopicSubscription(int userId, int topicId);
		Task ToggleForumSubscription(int userId, int forumId);
		Task StartSendingForumNotifications(int userId, int forumId);
		Task StartSendingTopicNotifications(int userId, int topicId);
		Task<bool> IsSubscribedToTopic(int userId, int topicId);
		Task<bool> IsSubscribedToForum(int userId, int forumId);
    }
}