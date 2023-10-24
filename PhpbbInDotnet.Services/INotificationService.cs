using System.Threading.Tasks;

namespace PhpbbInDotnet.Services
{
	public interface INotificationService
	{
		Task SendNewPostNotification(int authorId, int forumId, int topicId, int postId, string forumPath, string topicTitle);
	}
}