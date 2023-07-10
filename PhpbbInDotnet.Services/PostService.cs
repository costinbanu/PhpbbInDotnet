using Dapper;
using LazyCache;
using Microsoft.Extensions.Configuration;
using PhpbbInDotnet.Database.Entities;
using PhpbbInDotnet.Database.SqlExecuter;
using PhpbbInDotnet.Domain;
using PhpbbInDotnet.Domain.Extensions;
using PhpbbInDotnet.Domain.Utilities;
using PhpbbInDotnet.Objects;
using PhpbbInDotnet.Objects.Configuration;
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
        private readonly IAppCache _cache;
        private readonly int _maxAttachmentCount;
        private readonly ILogger _logger;

        public PostService(ISqlExecuter sqlExecuter, IAppCache cache, IConfiguration config, ILogger logger)
        {
            _sqlExecuter = sqlExecuter;
            _cache = cache;
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
                    _cache.Add(CacheUtility.GetAttachmentCacheKey(attachment.AttachId, correlationId), dto, TimeSpan.FromSeconds(60));
                    ids.Add(attachment.AttachId);
                }
            }

            if (!isPreview && ids.Any())
            {
                try
                {
                    await _sqlExecuter.ExecuteAsync(
                        "UPDATE phpbb_attachments SET download_count = download_count + 1 WHERE attach_id IN @ids",
                        new { ids });
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
            var posts = (await _sqlExecuter.CallStoredProcedureAsync<PostDto>(
                "get_posts",
                new
                {
                    topicId,
                    Constants.ANONYMOUS_USER_ID,
                    order = isPostingView ? "DESC" : "ASC",
                    skip = (pageNum - 1) * pageSize, 
                    take = pageSize
                })).AsList();

            var currentPostIds = posts.Select(p => p.PostId).DefaultIfEmpty().ToList();
            var dbAttachments = await _sqlExecuter.QueryAsync<PhpbbAttachments>(
                "SELECT * FROM phpbb_attachments WHERE post_msg_id IN @currentPostIds",
                new { currentPostIds });
            var reports = new List<ReportDto>();
            var count = null as int?;
            if (!isPostingView)
            {
                count = posts.FirstOrDefault()?.TotalCount ?? 0;
                reports = (await _sqlExecuter.QueryAsync<ReportDto>(
                    @"SELECT r.report_id AS id, 
		                     rr.reason_title, 
                             rr.reason_description, 
                             r.report_text AS details, 
                             r.user_id AS reporter_id, 
                             u.username AS reporter_username, 
                             r.post_id,
                             r.report_time,
                             r.report_closed
	                    FROM phpbb_reports r
                        JOIN phpbb_reports_reasons rr ON r.reason_id = rr.reason_id
                        JOIN phpbb_users u on r.user_id = u.user_id
	                   WHERE r.post_id IN @postIds;",
                    new { postIds = currentPostIds })).AsList();
            }

            var (CorrelationId, Attachments) = await CacheAttachmentsAndPrepareForDisplay(dbAttachments, posts.FirstOrDefault()?.ForumId ?? 0, language, currentPostIds.Count, false);

            return new PostListDto
            {
                Posts = posts.AsList(),
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

    }
}
