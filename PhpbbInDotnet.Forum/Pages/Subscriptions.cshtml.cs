using Dapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using PhpbbInDotnet.Database.SqlExecuter;
using PhpbbInDotnet.Domain;
using PhpbbInDotnet.Domain.Extensions;
using PhpbbInDotnet.Domain.Utilities;
using PhpbbInDotnet.Forum.Models;
using PhpbbInDotnet.Languages;
using PhpbbInDotnet.Objects;
using PhpbbInDotnet.Services;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace PhpbbInDotnet.Forum.Pages
{
	[ValidateAntiForgeryToken, ResponseCache(NoStore = true, Duration = 0)]
	public class SubscriptionsModel : AuthenticatedPageModel
	{
		const int PAGE_SIZE = 20;

		private readonly ILogger _logger;

		[BindProperty(SupportsGet = true)]
		public SubscriptionPageMode PageMode { get; set; } = SubscriptionPageMode.Forums;

		[BindProperty(SupportsGet = true)]
		public int ForumsPageNum { get; set; } = 1;

		[BindProperty(SupportsGet = true)]
		public int TopicsPageNum { get; set; } = 1;

		[BindProperty]
		public int[] ForumsToUnsubscribe { get; set; } = [];

		[BindProperty]
		public int[] TopicsToUnsubscribe { get; set; } = [];

		public List<ForumDto> ForumSubscriptions { get; private set; } = [];
		public List<TopicDto> TopicSubscriptions { get; private set; } = [];
		public Paginator? ForumsPaginator { get; private set; }
		public Paginator? TopicsPaginator { get; private set; }
		public string? MessageClass { get; private set; }
		public string? Message { get; private set; }

		public SubscriptionsModel(IForumTreeService forumService, IUserService userService, ISqlExecuter sqlExecuter, ITranslationProvider translationProvider,
			IConfiguration configuration, ILogger logger)
			: base(forumService, userService, sqlExecuter, translationProvider, configuration)
		{
			_logger = logger;
		}

		public Task<IActionResult> OnGet()
			=> WithRegisteredUser(async curUser =>
			{
				if (PageMode == SubscriptionPageMode.Forums)
				{
					await ResiliencyUtility.RetryOnceAsync(
						toDo: async () =>
						{
							var count = await SqlExecuter.ExecuteScalarAsync<int>(
								"SELECT count(forum_id) FROM phpbb_forums_watch WHERE user_id = @userId",
								new { curUser.UserId });

							ForumSubscriptions = (await SqlExecuter.WithPagination(PAGE_SIZE * (ForumsPageNum - 1), PAGE_SIZE).QueryAsync<ForumDto>(
								@"SELECT f.forum_id,
										 f.parent_id,
                                         f.forum_name, 
                                         f.forum_desc, 
                                         f.forum_last_post_id, 
                                         f.forum_last_post_time, 
                                         f.forum_last_poster_id, 
                                         f.forum_last_poster_name, 
                                         f.forum_last_poster_colour
                                    FROM phpbb_forums f
                                    JOIN phpbb_forums_watch fw ON f.forum_id = fw.forum_id
                                   WHERE fw.user_id = @userId
                                   ORDER BY f.forum_last_post_time DESC",
								new { curUser.UserId })).AsList();

							ForumsPaginator = new(count, ForumsPageNum, link: $"/Subscriptions?{nameof(PageMode)}={PageMode}&{nameof(TopicsPageNum)}={TopicsPageNum}", pageSize: PAGE_SIZE, pageNumKey: nameof(ForumsPageNum));
						},
						evaluateSuccess: () => ForumSubscriptions.Count > 0 && ForumsPageNum == ForumsPaginator?.CurrentPage,
						fix: () => ForumsPageNum = ForumsPaginator!.CurrentPage);
				}
				else if (PageMode == SubscriptionPageMode.Topics)
				{
					await ResiliencyUtility.RetryOnceAsync(
						toDo: async () =>
						{
							var count = await SqlExecuter.ExecuteScalarAsync<int>(
								"SELECT count(topic_id) FROM phpbb_topics_watch WHERE user_id = @userId",
								new { curUser.UserId });

							TopicSubscriptions = (await SqlExecuter.WithPagination(PAGE_SIZE * (TopicsPageNum - 1), PAGE_SIZE).QueryAsync<TopicDto>(
								@"SELECT t.forum_id, 
										 t.topic_id, 
										 t.topic_title, 
										 t.topic_last_poster_id, 
										 t.topic_last_poster_name, 
										 t.topic_last_poster_colour, 
										 t.topic_last_post_time, 
										 count(p.post_id) AS post_count
									FROM phpbb_topics t
									JOIN phpbb_topics_watch tw ON t.topic_id = tw.topic_id
									JOIN phpbb_posts p ON t.topic_id = p.topic_id
								   WHERE tw.user_id = @userId
								   GROUP BY t.forum_id, t.topic_id, t.topic_title, t.topic_last_poster_id, t.topic_last_poster_name, t.topic_last_poster_colour, t.topic_last_post_time
								   ORDER BY t.topic_last_post_time DESC",
								new { curUser.UserId })).AsList();

							TopicsPaginator = new(count, TopicsPageNum, link: $"/Subscriptions?{nameof(PageMode)}={PageMode}&{nameof(ForumsPageNum)}={ForumsPageNum}", pageSize: PAGE_SIZE, pageNumKey: nameof(TopicsPageNum));
						},
						evaluateSuccess: () => TopicSubscriptions.Count > 0 && TopicsPageNum == TopicsPaginator?.CurrentPage,
						fix: () => TopicsPageNum = TopicsPaginator!.CurrentPage);
				}

				return Page();
			});

		public Task<IActionResult> OnPost()
			=> WithRegisteredUser(async curUser =>
			{
				try
				{
					if (PageMode == SubscriptionPageMode.Forums)
					{
						if (TopicsToUnsubscribe.Any(t => t != default))
						{
							throw new InvalidOperationException("Can't unsubscribe from topics when page mode is set to forums.");
						}

						if (!ForumsToUnsubscribe.Any(f => f != default))
						{
							MessageClass = "message warning";
							Message = TranslationProvider.Errors[Language, "NO_VALID_FORUMS_SELECTED"];
						}
						else
						{
							var count = await SqlExecuter.ExecuteAsync(
								"DELETE FROM phpbb_forums_watch WHERE forum_id IN @forumsToUnsubscribe AND user_id = @userId",
								new { ForumsToUnsubscribe, curUser.UserId });
							MessageClass = "message success";
							Message = count == 1
								? TranslationProvider.BasicText[Language, "UNSUBSCRIBE_FROM_FORUM_SUCCESS"]
								: string.Format(TranslationProvider.BasicText[Language, "SUCESSFULLY_UNSUBSCRIBED_FROM_FORUMS_FORMAT"], count);
						}
					}
					else if (PageMode == SubscriptionPageMode.Topics)
					{
						if (ForumsToUnsubscribe.Any(x => x != default))
						{
							throw new InvalidOperationException("Can't unsubscribe from forums when page mode is set to topics.");
						}

						if (!TopicsToUnsubscribe.Any(f => f != default))
						{
							MessageClass = "message warning";
							Message = TranslationProvider.Errors[Language, "NO_VALID_TOPICS_SELECTED"];
						}
						else
						{
							var count = await SqlExecuter.ExecuteAsync(
								"DELETE FROM phpbb_topics_watch WHERE topic_id IN @topicsToUnsubscribe AND user_id = @userId",
								new { TopicsToUnsubscribe, curUser.UserId });
							MessageClass = "message success";
							Message = count == 1
								? TranslationProvider.BasicText[Language, "UNSUBSCRIBE_FROM_TOPIC_SUCCESS"]
								: string.Format(TranslationProvider.BasicText[Language, "SUCESSFULLY_UNSUBSCRIBED_FROM_TOPICS_FORMAT"], count);
						}
					}
				}
				catch (Exception ex)
				{
					var id = _logger.ErrorWithId(ex);
					MessageClass = "message error";
					Message = string.Format(TranslationProvider.Errors[Language, "AN_ERROR_OCCURRED_TRY_AGAIN_ID_FORMAT"], id);
				}

				return await OnGet();
			});
	}
}
