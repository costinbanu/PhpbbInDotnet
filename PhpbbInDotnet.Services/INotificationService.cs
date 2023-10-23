using System.Threading.Tasks;

namespace PhpbbInDotnet.Services
{
	public interface INotificationService
	{
		Task SendNewPostNotification(int topicId, int postId, string path);
	}
}