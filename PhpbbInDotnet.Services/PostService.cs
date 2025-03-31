using Dapper;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using PhpbbInDotnet.Database.Entities;
using PhpbbInDotnet.Database.SqlExecuter;
using PhpbbInDotnet.Domain;
using PhpbbInDotnet.Domain.Extensions;
using PhpbbInDotnet.Domain.Utilities;
using PhpbbInDotnet.Objects;
using PhpbbInDotnet.Objects.Configuration;
using PhpbbInDotnet.Services.Caching;
using Serilog;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;

namespace PhpbbInDotnet.Services
{
    class PostService : IPostService
    {
        private readonly ISqlExecuter _sqlExecuter;
        private readonly IDistributedCache _cache;
		private readonly ICachedDbInfoService _cachedDbInfoService;
		private readonly int _maxAttachmentCount;
        private readonly ILogger _logger;

        public PostService(ISqlExecuter sqlExecuter, IDistributedCache cache, ICachedDbInfoService cachedDbInfoService, IConfiguration config, ILogger logger)
        {
            _sqlExecuter = sqlExecuter;
            _cache = cache;
            _cachedDbInfoService = cachedDbInfoService;
            var countLimit = config.GetObject<AttachmentLimits>("UploadLimitsCount");
            _maxAttachmentCount = Math.Max(countLimit.Images, countLimit.OtherFiles);
            _logger = logger;
        }

        public async Task<(Guid CorrelationId, Dictionary<int, List<AttachmentDto>> Attachments)> CacheAttachmentsAndPrepareForDisplay(IEnumerable<PhpbbAttachmentExpanded> dbAttachments, string language, int postCount, bool isPreview)
        {
            var correlationId = Guid.NewGuid();
            var attachments = new Dictionary<int, List<AttachmentDto>>(postCount);
            var ids = new List<int>();
            foreach (var attachment in dbAttachments)
            {
                var dto = new AttachmentDto(attachment, attachment.ForumId, isPreview, language, correlationId);
                if (!attachments.ContainsKey(attachment.PostMsgId))
                {
                    attachments.Add(attachment.PostMsgId, new List<AttachmentDto>(_maxAttachmentCount) { dto });
                }
                else
                {
                    attachments[attachment.PostMsgId].Add(dto);
                }
                if (StringUtility.IsMimeTypeInline(attachment.Mimetype))
                {
                    _cache.Set(
                        key: CacheUtility.GetAttachmentCacheKey(attachment.AttachId, correlationId), 
                        value: await CompressionUtility.CompressObject(dto), 
                        options: new DistributedCacheEntryOptions
                        {
                            SlidingExpiration = TimeSpan.FromSeconds(60)
                        });
                    ids.Add(attachment.AttachId);
                }
            }

            if (!isPreview && ids.Any())
            {
                try
                {
                    await _sqlExecuter.ExecuteAsyncWithoutResiliency(
                        "UPDATE phpbb_attachments SET download_count = download_count + 1 WHERE attach_id IN @ids",
                        new { ids },
                        commandTimeout: 10);
                }
                catch (Exception ex)
                {
                    _logger.WarningWithId(ex, "Error updating attachment download count");
                }
            }

            return (correlationId, attachments);
        }

        public Task<(Guid CorrelationId, Dictionary<int, List<AttachmentDto>> Attachments)> CacheAttachmentsAndPrepareForDisplay(IEnumerable<PhpbbAttachments> dbAttachments, int forumId, string language, int postCount, bool isPreview)
            => CacheAttachmentsAndPrepareForDisplay(dbAttachments.Select(attachment => new PhpbbAttachmentExpanded(attachment, forumId)), language, postCount, isPreview);

        public async Task<PostListDto> GetPosts(int topicId, int pageNum, int pageSize, bool isPostingView, string language)
        {
            using var results = await _sqlExecuter.CallMultipleResultsStoredProcedureAsync(
                "get_posts",
                new
                {
                    topicId,
                    Constants.ANONYMOUS_USER_ID,
                    order = isPostingView ? "DESC" : "ASC",
                    skip = (pageNum - 1) * pageSize, 
                    take = pageSize,
                    includeReports = !isPostingView
                });

            var posts = (await results.ReadAsync<PostDto>()).AsList();
            var dbAttachments = await results.ReadAsync<PhpbbAttachments>();
            var reports = new List<ReportDto>();
            var count = null as int?;
            if (!isPostingView)
            {
                count = posts.FirstOrDefault()?.TotalCount ?? 0;
                reports = (await results.ReadAsync<ReportDto>()).AsList();
            }

            var (CorrelationId, Attachments) = await CacheAttachmentsAndPrepareForDisplay(dbAttachments, posts.FirstOrDefault()?.ForumId ?? 0, language, posts.Count, false);

            return new PostListDto
            {
                Posts = posts,
                Attachments = Attachments,
                AttachmentDisplayCorrelationId = CorrelationId,
                PostCount = count,
                Reports = reports
            };
        }

        public async Task<PollDto?> GetPoll(PhpbbTopics _currentTopic)
        {
            var options = Enumerable.Empty<PhpbbPollOptions>();
            var voters = Enumerable.Empty<PollOptionVoter>();

            options = await _sqlExecuter.QueryAsync<PhpbbPollOptions>("SELECT * FROM phpbb_poll_options WHERE topic_id = @TopicId ORDER BY poll_option_id", new { _currentTopic.TopicId });
            if (options.Any())
            {
                voters = await _sqlExecuter.QueryAsync<PollOptionVoter>(
                    @"SELECT u.user_id, u.username, v.poll_option_id
                        FROM phpbb_users u
                        JOIN phpbb_poll_votes v ON u.user_id = v.vote_user_id
                        WHERE v.poll_option_id IN @optionIds
                            AND v.topic_id IN @topicIds",
                    new { optionIds = options.Select(o => o.PollOptionId).DefaultIfEmpty(), topicIds = options.Select(o => o.TopicId).DefaultIfEmpty() }
                );
            }
            else
            {
                return null;
            }

            return new PollDto
            {
                PollTitle = _currentTopic.PollTitle,
                PollStart = _currentTopic.PollStart.ToUtcTime(),
                PollDurationSecons = _currentTopic.PollLength,
                PollMaxOptions = _currentTopic.PollMaxOptions,
                TopicId = _currentTopic.TopicId,
                VoteCanBeChanged = _currentTopic.PollVoteChange.ToBool(),
                PollOptions = options.Select(o =>
                new PollOption
                {
                    PollOptionId = o.PollOptionId,
                    PollOptionText = o.PollOptionText,
                    TopicId = o.TopicId,
                    PollOptionVoters = voters.Where(v => v.PollOptionId == o.PollOptionId).ToList()
                }).ToList()
            };
        }

		public Task CascadePostEdit(PhpbbPosts edited, ITransactionalSqlExecuter transaction)
			=>  SyncTopicWithPosts(transaction, edited.TopicId);

		public Task CascadePostAdd(ITransactionalSqlExecuter transaction, bool ignoreUser, bool ignoreForums, params PhpbbPosts[] added)
			=> CascadePostAdd(transaction, ignoreUser, ignoreForums, added.AsEnumerable());

		public async Task CascadePostAdd(ITransactionalSqlExecuter transaction, bool ignoreUser, bool ignoreForums, IEnumerable<PhpbbPosts> added)
		{
            if (!added.Any())
            {
                return;
            }

			if (!ignoreForums)
			{
				await SyncForumWithPosts(transaction, added.Select(p => p.ForumId));
			}

			await SyncTopicWithPosts(transaction, added.Select(p => p.TopicId));

			if (!ignoreUser)
			{
				await SyncUserPostCount(transaction, added.Select(p => p.PostId), isDeleted: false);
			}
		}

		public async Task CascadePostDelete(ITransactionalSqlExecuter transaction, bool ignoreUser, bool ignoreForums, bool ignoreTopics, bool ignoreAttachmentsAndReports, IEnumerable<PhpbbPosts> deleted)
		{
            if (!deleted.Any())
            {
                return;
            }

			if (!ignoreTopics)
			{
				await SyncTopicWithPosts(transaction, deleted.Select(p => p.TopicId));
			}

			if (!ignoreForums)
			{
				await SyncForumWithPosts(transaction, deleted.Select(p => p.ForumId));
			}

			if (!ignoreAttachmentsAndReports)
			{
				await transaction.ExecuteAsync(
					"DELETE FROM phpbb_reports WHERE post_id IN @postIds; " +
					"DELETE FROM phpbb_attachments WHERE post_msg_id IN @postIds",
					new { postIds = deleted.Select(d => d.PostId) });
			}

			if (!ignoreUser)
			{
                await SyncUserPostCount(transaction, deleted.Select(p => p.PostId), isDeleted: true);
			}
		}

		public Task SyncForumWithPosts(ITransactionalSqlExecuter transaction, params int[] forumIds)
			=> SyncForumWithPosts(transaction, forumIds.AsEnumerable());

        private async Task SyncForumWithPosts(ITransactionalSqlExecuter transaction, IEnumerable<int> forumIds)
        {
            var actualForumIds = forumIds.Distinct();
            if (!actualForumIds.Any())
            {
                return;
            }

            await transaction.CallStoredProcedureAsync(
                "sync_forum_with_posts",
                new
                {
                    forumIds = string.Join(",", actualForumIds),
                    Constants.ANONYMOUS_USER_ID,
                    Constants.ANONYMOUS_USER_NAME,
                    Constants.DEFAULT_USER_COLOR
                });
		}

		private Task SyncTopicWithPosts(ITransactionalSqlExecuter transaction, params int[] topicIds)
	        => SyncTopicWithPosts(transaction, topicIds.AsEnumerable());

        private async Task SyncTopicWithPosts(ITransactionalSqlExecuter transaction, IEnumerable<int> topicIds)
        {
			var actualTopicIds = topicIds.Distinct();
			if (!actualTopicIds.Any())
			{
				return;
			}

			await transaction.CallStoredProcedureAsync(
                "sync_topic_with_posts",
                new
                {
                    topicIds = string.Join(',', actualTopicIds),
                    Constants.ANONYMOUS_USER_ID,
                    Constants.ANONYMOUS_USER_NAME,
                    Constants.DEFAULT_USER_COLOR
                });
		}

        private static async Task SyncUserPostCount(ITransactionalSqlExecuter transaction, IEnumerable<int> postIds, bool isDeleted)
        {
			var actualPostIds = postIds.Distinct();
			if (!actualPostIds.Any())
			{
				return;
			}

			await transaction.CallStoredProcedureAsync(
                "adjust_user_post_count",
                new
                {
                    postIds = string.Join(',', actualPostIds),
                    postOperation = isDeleted ? "delete" : "add"
                });
        }
	}
}
