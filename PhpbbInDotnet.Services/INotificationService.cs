using System.Threading.Tasks;

namespace PhpbbInDotnet.Services
{
	public interface INotificationService
	{
		Task SendNewPostNotification(int authorId, int topicId, int postId, string path);
	}
}