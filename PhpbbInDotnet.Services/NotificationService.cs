using Microsoft.Extensions.Configuration;
using PhpbbInDotnet.Database.Entities;
using PhpbbInDotnet.Database.SqlExecuter;
using PhpbbInDotnet.Languages;
using PhpbbInDotnet.Objects.EmailDtos;
using System.Linq;
using System.Threading.Tasks;

namespace PhpbbInDotnet.Services
{
	class NotificationService : INotificationService
	{
		private readonly ISqlExecuter _sqlExecuter;
		private readonly IEmailService _emailService;
		private readonly ITranslationProvider _translationProvider;
		private readonly IConfiguration _configuration;

		public NotificationService(ISqlExecuter sqlExecuter, IEmailService emailService, ITranslationProvider translationProvider, IConfiguration configuration)
		{
			_sqlExecuter = sqlExecuter;
			_emailService = emailService;
			_translationProvider = translationProvider;
			_configuration = configuration;
		}

		public async Task SendNewPostNotification(int topicId, int postId, string path)
		{
			var topicSubscribers = await _sqlExecuter.QueryAsync<PhpbbUsers>(
				@"SELECT u.*
                    FROM phpbb_users u
                    JOIN phpbb_topics_watch tw ON u.user_id = tw.user_id
                   WHERE tw.topic_id = @topicId 
                     AND tw.notify_status = 0",
				new { topicId });

			await Task.WhenAll(topicSubscribers.Select(subscriber => _emailService.SendEmail(
				to: subscriber.UserEmail,
				subject: string.Format(_translationProvider.Email[subscriber.UserLang, "NEW_POST_SUBJECT_FORMAT"], _configuration.GetValue<string>("ForumName")),
				bodyRazorViewName: "_NewPostInTopicNotification",
				bodyRazorViewModel: new NewPostNotificationDto(language: subscriber.UserLang, userName: subscriber.Username, postId, topicId, path))));

			await _sqlExecuter.ExecuteAsync(
				"UPDATE phpbb_topics_watch SET notify_status = 1 WHERE user_id IN @users AND topic_id = @topicId",
				new { users = topicSubscribers.Select(s => s.UserId).DefaultIfEmpty(), topicId });

		}
	}
}
