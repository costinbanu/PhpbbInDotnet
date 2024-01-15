using Microsoft.Extensions.Configuration;
using PhpbbInDotnet.Database.Entities;
using PhpbbInDotnet.Database.SqlExecuter;
using PhpbbInDotnet.Domain.Extensions;
using PhpbbInDotnet.Languages;
using PhpbbInDotnet.Objects.EmailDtos;
using Serilog;
using System;
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
        private readonly ILogger _logger;

        public NotificationService(ISqlExecuter sqlExecuter, IEmailService emailService, ITranslationProvider translationProvider, IConfiguration configuration, ILogger logger)
		{
			_sqlExecuter = sqlExecuter;
			_emailService = emailService;
			_translationProvider = translationProvider;
			_configuration = configuration;
			_logger = logger;
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

		public async Task<(string Message, bool IsSuccess)> ToggleTopicSubscription(int userId, int topicId)
		{
			var lang = _translationProvider.GetLanguage();
			string message;

			try
			{
				var affectedRows = await _sqlExecuter.ExecuteAsync(
					"DELETE FROM phpbb_topics_watch WHERE user_id = @userId AND topic_id = @topicId",
					new { userId, topicId });

				if (affectedRows == 0)
				{
					await _sqlExecuter.ExecuteAsync(
						"INSERT INTO phpbb_topics_watch(user_id, topic_id, notify_status) VALUES (@userId, @topicId, 0)",
						new { userId, topicId });
                    message = _translationProvider.BasicText[lang, "SUBSCRIBE_TO_TOPIC_SUCCESS"];
                }
                else
                {
                    message = _translationProvider.BasicText[lang, "UNSUBSCRIBE_FROM_TOPIC_SUCCESS"];
                }

				return (message, true);
            }
			catch (Exception ex)
			{
                var id = _logger.ErrorWithId(ex);
                message = string.Format(_translationProvider.Errors[lang, "AN_ERROR_OCCURRED_TRY_AGAIN_ID_FORMAT"], id);

                return (message, false);
            }
        }

		public async Task<(string Message, bool IsSuccess)> ToggleForumSubscription(int userId, int forumId)
		{
			var lang = _translationProvider.GetLanguage();
			string message;

			try
			{
				var affectedRows = await _sqlExecuter.ExecuteAsync(
					"DELETE FROM phpbb_forums_watch WHERE user_id = @userId AND forum_id = @forumId",
					new { userId, forumId });

				if (affectedRows == 0)
				{
					await _sqlExecuter.ExecuteAsync(
						"INSERT INTO phpbb_forums_watch(user_id, forum_id, notify_status) VALUES (@userId, @forumId, 0)",
						new { userId, forumId });
					message = _translationProvider.BasicText[lang, "SUBSCRIBE_TO_FORUM_SUCCESS"];
                }
				else
				{
					message = _translationProvider.BasicText[lang, "UNSUBSCRIBE_FROM_FORUM_SUCCESS"];
                }

				return (message, true);
			}
			catch (Exception ex)
			{
				var id = _logger.ErrorWithId(ex);
				message = string.Format(_translationProvider.Errors[lang, "AN_ERROR_OCCURRED_TRY_AGAIN_ID_FORMAT"], id);

				return (message, false);
			}
        }

		public Task StartSendingForumNotifications(int userId, int forumId)
			=> _sqlExecuter.ExecuteAsyncWithoutResiliency(
                    "UPDATE phpbb_forums_watch SET notify_status = 0 WHERE forum_id = @forumId AND user_id = @userId",
                    new { forumId, userId });

        public Task StartSendingTopicNotifications(int userId, int topicId)
			=> _sqlExecuter.ExecuteAsyncWithoutResiliency(
                    "UPDATE phpbb_topics_watch SET notify_status = 0 WHERE topic_id = @topicId AND user_id = @userId",
                    new { topicId, userId });

		public async Task<bool> IsSubscribedToTopic(int userId, int topicId)
		{
			var result = await _sqlExecuter.QueryFirstOrDefaultAsync(
				"SELECT 1 FROM phpbb_topics_watch WHERE user_id = @userId AND topic_id = @topicId",
				new { userId, topicId });

			return result is not null;
		}

        public async Task<bool> IsSubscribedToForum(int userId, int forumId)
        {
            var result = await _sqlExecuter.QueryFirstOrDefaultAsync(
                "SELECT 1 FROM phpbb_forums_watch WHERE user_id = @userId AND forum_id = @forumId",
                new { userId, forumId });

            return result is not null;
        }
    }
}
