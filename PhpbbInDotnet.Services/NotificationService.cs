using Microsoft.Extensions.Configuration;
using PhpbbInDotnet.Database.Entities;
using PhpbbInDotnet.Database.SqlExecuter;
using PhpbbInDotnet.Languages;
using PhpbbInDotnet.Objects.EmailDtos;
using System.Collections.Generic;
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

		public async Task SendNewPostNotification(int authorId, int forumId, int topicId, int postId, string forumPath, string topicTitle)
		{
			var forumSubscribersStatement = 
				@"SELECT u.*
					FROM phpbb_users u
					JOIN phpbb_forums_watch fw ON u.user_id = fw.user_id
					LEFT JOIN phpbb_zebra z ON u.user_id = z.user_id
				   WHERE u.user_id <> @authorId
					 AND fw.forum_id = @forumId 
					 AND (z.zebra_id IS NULL OR z.zebra_id <> @authorId OR z.foe <> 1)";
			var forumSubscribersParams = new { forumId, authorId };

            using (var forumTransaction = _sqlExecuter.BeginTransaction())
			{
                var forumSubscribers = await forumTransaction.QueryAsync<PhpbbUsers>($"{forumSubscribersStatement} AND fw.notify_status = 0;", forumSubscribersParams);

                await Task.WhenAll(forumSubscribers.Select(s => 
					_emailService.SendEmail(
						to: s.UserEmail,
						subject: string.Format(_translationProvider.Email[s.UserLang, "NEW_POST_SUBJECT_FORMAT"], _configuration.GetValue<string>("ForumName")),
						bodyRazorViewName: "_NewPostNotification",
						bodyRazorViewModel: new NewPostNotificationDto(language: s.UserLang, userName: s.Username, forumId, forumPath))));

                await forumTransaction.ExecuteAsync(
                    "UPDATE phpbb_forums_watch SET notify_status = 1 WHERE user_id IN @users AND forum_id = @forumId",
                    new { users = forumSubscribers.Select(s => s.UserId).DefaultIfEmpty(), forumId });

                forumTransaction.CommitTransaction();
			}

            using var topicTransaction = _sqlExecuter.BeginTransaction();
            var topicSubscribers = await topicTransaction.QueryAsync<PhpbbUsers>(
                @"SELECT u.*
					FROM phpbb_users u
					JOIN phpbb_topics_watch tw ON u.user_id = tw.user_id
					LEFT JOIN phpbb_zebra z ON u.user_id = z.user_id
				   WHERE u.user_id <> @authorId
					 AND tw.topic_id = @topicId 
					 AND tw.notify_status = 0
					 AND (z.zebra_id IS NULL OR z.zebra_id <> @authorId OR z.foe <> 1)",
                new { topicId, authorId });

            var forumNotificationCandidates = await topicTransaction.QueryAsync<PhpbbUsers>(forumSubscribersStatement, forumSubscribersParams);
            var forumNotificationCandidateIds = new HashSet<int>(forumNotificationCandidates.Select(c => c.UserId));
			var topicNotificationUsers = topicSubscribers.Where(s => !forumNotificationCandidateIds.Contains(s.UserId));

            await Task.WhenAll(topicNotificationUsers.Select(s => 
				_emailService.SendEmail(
                    to: s.UserEmail,
                    subject: string.Format(_translationProvider.Email[s.UserLang, "NEW_POST_SUBJECT_FORMAT"], _configuration.GetValue<string>("ForumName")),
                    bodyRazorViewName: "_NewPostNotification",
                    bodyRazorViewModel: new NewPostNotificationDto(language: s.UserLang, userName: s.Username, postId, topicId, forumPath, topicTitle))));

            await topicTransaction.ExecuteAsync(
                "UPDATE phpbb_topics_watch SET notify_status = 1 WHERE user_id IN @users AND topic_id = @topicId",
                new { users = topicNotificationUsers.Select(s => s.UserId).DefaultIfEmpty(), topicId });

            topicTransaction.CommitTransaction();
        }
	}
}
