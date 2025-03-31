using Dapper;
using PhpbbInDotnet.Database.Entities;
using PhpbbInDotnet.Database.SqlExecuter;
using PhpbbInDotnet.Domain;
using PhpbbInDotnet.Domain.Extensions;
using PhpbbInDotnet.Domain.Utilities;
using PhpbbInDotnet.Languages;
using PhpbbInDotnet.Objects;
using PhpbbInDotnet.Services.Storage;
using Serilog;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;

namespace PhpbbInDotnet.Services
{
	class ModeratorService : IModeratorService
    {
        private readonly ISqlExecuter _sqlExecuter;
        private readonly IPostService _postService;
        private readonly IStorageService _storageService;
        private readonly IOperationLogService _operationLogService;
        private readonly ILogger _logger;
		private readonly ITranslationProvider _translationProvider;

        public ModeratorService(ISqlExecuter sqlExecuter, IPostService postService, IStorageService storageService, ITranslationProvider translationProvider,
            IOperationLogService operationLogService, ILogger logger)
        {
            _translationProvider = translationProvider;
            _sqlExecuter = sqlExecuter;
            _postService = postService;
            _storageService = storageService;
            _operationLogService = operationLogService;
            _logger = logger;
        }

        #region Topic

        public async Task<(string Message, bool? IsSuccess)> ChangeTopicType(int topicId, TopicType topicType, OperationLogDto logDto)
        {
            var language = _translationProvider.GetLanguage();
            try
            {
                var rows = await _sqlExecuter.ExecuteAsync("UPDATE phpbb_topics SET topic_type = @topicType WHERE topic_id = @topicId", new { topicType, topicId });

                if (rows == 1)
                {
                    await _operationLogService.LogModeratorTopicAction((ModeratorTopicActions)logDto.Action!, logDto.UserId, topicId);
                    return (_translationProvider.Moderator[language, "TOPIC_CHANGED_SUCCESSFULLY"], true);
                }
                else
                {
                    return (string.Format(_translationProvider.Moderator[language, "TOPIC_DOESNT_EXIST_FORMAT"], topicId), false);
                }

            }
            catch (Exception ex)
            {
                var id = _logger.ErrorWithId(ex);
                return (string.Format(_translationProvider.Errors[language, "AN_ERROR_OCCURRED_TRY_AGAIN_ID_FORMAT"], id), false);
            }
        }

        public async Task<(string Message, bool? IsSuccess)> MoveTopic(int topicId, int destinationForumId, OperationLogDto logDto)
        {
            var language = _translationProvider.GetLanguage();
            try
            {
                using var transaction = _sqlExecuter.BeginTransaction(IsolationLevel.Serializable);
                var topicRows = await transaction.ExecuteAsync(
                    "UPDATE phpbb_topics SET forum_id = @destinationForumId WHERE topic_id = @topicID AND EXISTS(SELECT 1 FROM phpbb_forums WHERE forum_id = @destinationForumId)",
                    new { topicId, destinationForumId });

                if (topicRows == 0)
                {
                    return (_translationProvider.Moderator[language, "DESTINATION_DOESNT_EXIST"], false);
                }

                var oldPosts = (await transaction.QueryAsync<PhpbbPosts>(
                    "SELECT * FROM phpbb_posts WHERE topic_id = @topicId ORDER BY post_time DESC", 
                    new { topicId })).AsList();
                var oldForumId = oldPosts.FirstOrDefault()?.ForumId ?? 0;
                await transaction.ExecuteAsync(
                    "UPDATE phpbb_posts SET forum_id = @destinationForumId WHERE topic_id = @topicId; " +
                    "UPDATE phpbb_topics_track SET forum_id = @destinationForumId WHERE topic_id = @topicId",
                    new { destinationForumId, topicId });

                await _postService.SyncForumWithPosts(transaction, oldForumId, destinationForumId);

				await _operationLogService.LogModeratorTopicAction(ModeratorTopicActions.MoveTopic, logDto.UserId, topicId, $"Moved from {oldForumId} to {destinationForumId}.", transaction);

                await transaction.CommitTransaction();

                return (_translationProvider.Moderator[language, "TOPIC_CHANGED_SUCCESSFULLY"], true);
            }
            catch (Exception ex)
            {
                var id = _logger.ErrorWithId(ex);
                return (string.Format(_translationProvider.Errors[language, "AN_ERROR_OCCURRED_TRY_AGAIN_ID_FORMAT"], id), false);
            }
        }

        public async Task<(string Message, bool? IsSuccess)> LockUnlockTopic(int topicId, bool @lock, OperationLogDto logDto)
        {
            var language = _translationProvider.GetLanguage();
            try
            {
                var rows = await _sqlExecuter.ExecuteAsync("UPDATE phpbb_topics SET topic_status = @status WHERE topic_id = @topicId", new { status = @lock.ToByte(), topicId });

                if (rows == 0)
                {
                    return (string.Format(_translationProvider.Moderator[language, "TOPIC_DOESNT_EXIST_FORMAT"], topicId), false);
                }
                await _operationLogService.LogModeratorTopicAction((ModeratorTopicActions)logDto.Action!, logDto.UserId, topicId);

                return (_translationProvider.Moderator[language, "TOPIC_CHANGED_SUCCESSFULLY"], true);
            }
            catch (Exception ex)
            {
                var id = _logger.ErrorWithId(ex);
                return (string.Format(_translationProvider.Errors[language, "AN_ERROR_OCCURRED_TRY_AGAIN_ID_FORMAT"], id), false);
            }
        }

        public async Task<(string Message, bool? IsSuccess)> DeleteTopic(int topicId, OperationLogDto logDto)
        {
            var language = _translationProvider.GetLanguage();
            try
            {
                using var transaction = _sqlExecuter.BeginTransaction(IsolationLevel.Serializable);
                var posts = (await transaction.QueryAsync<PhpbbPosts>(
                    "SELECT * FROM phpbb_posts WHERE topic_id = @topicId", 
                    new { topicId })).AsList();

                if (!posts.Any())
                {
                    return (string.Format(_translationProvider.Moderator[language, "TOPIC_DOESNT_EXIST_FORMAT"], topicId), false);
                }

                var topic = await transaction.QueryFirstOrDefaultAsync<PhpbbTopics>("SELECT * FROM phpbb_topics WHERE topic_id = @topicId", new { topicId });
                if (topic != null)
                {
                    var dto = new TopicDto
                    {
                        ForumId = topic.ForumId,
                        TopicId = topic.TopicId,
                        TopicTitle = topic.TopicTitle,
                        TopicStatus = topic.TopicStatus,
                        TopicType = topic.TopicType,
                        TopicLastPosterColour = topic.TopicLastPosterColour,
                        TopicLastPosterId = topic.TopicLastPosterId,
                        TopicLastPosterName = topic.TopicLastPosterName,
                        TopicLastPostId = topic.TopicLastPostId,
                        TopicLastPostTime = topic.TopicLastPostTime,
                        Poll = await _postService.GetPoll(topic)
                    };
                    await transaction.ExecuteAsync(
                        "INSERT INTO phpbb_recycle_bin(type, id, content, delete_time, delete_user) VALUES (@type, @id, @content, @now, @userId)",
                        new
                        {
                            type = RecycleBinItemType.Topic,
                            id = topic.TopicId,
                            content = await CompressionUtility.CompressObject(dto),
                            now = DateTime.UtcNow.ToUnixTimestamp(),
                            logDto.UserId
                        });

                    await transaction.ExecuteAsync(
                        @"DELETE FROM phpbb_topics WHERE topic_id = @topicId; 
                          DELETE FROM phpbb_poll_options WHERE topic_id = @topicId;
                          DELETE FROM phpbb_topics_watch WHERE topic_id = @topicId",
                        new { topicId });
                }

                await DeletePostsCore(posts, logDto, shouldLog: false, ignoreTopics: true, transaction);

                await _operationLogService.LogModeratorTopicAction(ModeratorTopicActions.DeleteTopic, logDto.UserId, topicId, transaction: transaction);

                await transaction.CommitTransaction();

                return (_translationProvider.Moderator[language, "TOPIC_DELETED_SUCCESSFULLY"], true);
            }
            catch (Exception ex)
            {
                var id = _logger.ErrorWithId(ex);
                return (string.Format(_translationProvider.Errors[language, "AN_ERROR_OCCURRED_TRY_AGAIN_ID_FORMAT"], id), false);
            }
        }

        public async Task<(string Message, bool? IsSuccess)> CreateShortcut(int topicId, int forumId, OperationLogDto logDto)
        {
            var language = _translationProvider.GetLanguage();
            try
            {
                var curTopic = await _sqlExecuter.QueryFirstOrDefaultAsync<PhpbbTopics>(
                    "SELECT * FROM phpbb_topics WHERE topic_id = @topicId",
                    new { topicId });

                if (curTopic is null)
                {
                    return (string.Format(_translationProvider.Moderator[language, "TOPIC_DOESNT_EXIST_FORMAT"], topicId), false);
                }

                if (curTopic.ForumId == forumId)
                {
                    return (_translationProvider.Moderator[language, "INVALID_DESTINATION_FORUM"], false);
                }

                await _sqlExecuter.ExecuteAsync(
                    "INSERT INTO phpbb_shortcuts (topic_id, forum_id) VALUES(@topicId, @forumId)",
                    new { topicId, forumId });

                await _operationLogService.LogModeratorTopicAction(ModeratorTopicActions.CreateShortcut, logDto.UserId, topicId);

                return (_translationProvider.Moderator[language, "SHORTCUT_CREATED_SUCCESSFULLY"], true);
            }
            catch (Exception ex)
            {
                var id = _logger.ErrorWithId(ex);
                return (string.Format(_translationProvider.Errors[language, "AN_ERROR_OCCURRED_TRY_AGAIN_ID_FORMAT"], id), false);
            }
        }

        public async Task<(string Message, bool? IsSuccess)> RemoveShortcut(int topicId, int forumId, OperationLogDto logDto)
        {
            var language = _translationProvider.GetLanguage();
            try
            {
                var curShortcut = await _sqlExecuter.QueryFirstOrDefaultAsync<PhpbbShortcuts>(
                    "SELECT * FROM phpbb_shortcuts WHERE topic_id = @topicId",
                    new { topicId });

                if (curShortcut is null)
                {
                    return (string.Format(_translationProvider.Moderator[language, "SHORTCUT_DOESNT_EXIST_FORMAT"], topicId, forumId), false);
                }

                if (curShortcut.ForumId != forumId)
                {
                    return (_translationProvider.Moderator[language, "INVALID_SHORTCUT_SELECTED"], false);
                }

                await _sqlExecuter.ExecuteAsync(
                    "DELETE FROM phpbb_shortcuts WHERE topic_id = @topicId AND forum_id = @forumId",
                    new { topicId, forumId });

                await _operationLogService.LogModeratorTopicAction(ModeratorTopicActions.CreateShortcut, logDto.UserId, topicId);

                return (_translationProvider.Moderator[language, "SHORTCUT_DELETED_SUCCESSFULLY"], true);
            }
            catch (Exception ex)
            {
                var id = _logger.ErrorWithId(ex);
                return (string.Format(_translationProvider.Errors[language, "AN_ERROR_OCCURRED_TRY_AGAIN_ID_FORMAT"], id), false);
            }
        }

        #endregion Topic

        #region Post

        public async Task<(string Message, bool? IsSuccess)> SplitPosts(int[] postIds, int? destinationForumId, OperationLogDto logDto)
        {
            var language = _translationProvider.GetLanguage();
            try
            {
                if ((destinationForumId ?? 0) == 0)
                {
                    return (_translationProvider.Moderator[language, "INVALID_DESTINATION_FORUM"], false);
                }

                if (!(postIds?.Any() ?? false))
                {
                    return (_translationProvider.Moderator[language, "ATLEAST_ONE_POST_REQUIRED"], false);
                }

                using var transaction = _sqlExecuter.BeginTransaction(IsolationLevel.Serializable);
                var posts = (await transaction.QueryAsync<PhpbbPosts>(
                    "SELECT * FROM phpbb_posts WHERE post_id IN @postIds ORDER BY post_time", 
                    new { postIds })).AsList();

                if (posts.Count != postIds.Length)
                {
                    return (_translationProvider.Moderator[language, "ATLEAST_ONE_POST_MOVED_OR_DELETED"], false);
                }

                var newTopic = await transaction.QuerySingleAsync<PhpbbTopics>(
                    $@"INSERT INTO phpbb_topics (forum_id, topic_title, topic_time) VALUES (@forumId, @title, @time);
                       SELECT * FROM phpbb_topics WHERE topic_id = {_sqlExecuter.LastInsertedItemId};",
                    new 
                    { 
                        forumId = destinationForumId!.Value, 
                        title = posts.First().PostSubject, 
                        time = posts.First().PostTime 
                    });

                var oldTopicId = posts[0].TopicId;
                var oldForumId = posts[0].ForumId;

                await transaction.ExecuteAsync(
                    "UPDATE phpbb_posts SET topic_id = @topicId, forum_id = @forumId WHERE post_id IN @postIds", 
                    new { newTopic.TopicId, newTopic.ForumId, postIds });

                await _postService.CascadePostDelete(transaction, ignoreUser: true, ignoreForums: oldForumId == newTopic.ForumId, ignoreTopics: false, ignoreAttachmentsAndReports: true, posts);
              
                foreach (var post in posts)
                {
                    post.TopicId = newTopic.TopicId;
                    post.ForumId = newTopic.ForumId;
                    await _operationLogService.LogModeratorPostAction(ModeratorPostActions.SplitSelectedPosts, logDto.UserId, post.PostId, $"Split from topic {oldTopicId} as new topic in forum {destinationForumId}", transaction);
                }

                await _postService.CascadePostAdd(transaction, ignoreUser: true, ignoreForums: oldForumId == newTopic.ForumId, posts);

				await transaction.CommitTransaction();

                return (_translationProvider.Moderator[language, "POSTS_SPLIT_SUCCESSFULLY"], true);
            }
            catch (Exception ex)
            {
                var id = _logger.ErrorWithId(ex);
                return (string.Format(_translationProvider.Errors[language, "AN_ERROR_OCCURRED_TRY_AGAIN_ID_FORMAT"], id), false);
            }
        }

        public async Task<(string Message, bool? IsSuccess)> MovePosts(int[] postIds, int? destinationTopicId, OperationLogDto logDto)
        {
            var language = _translationProvider.GetLanguage();
            try
            {
                if ((destinationTopicId ?? 0) == 0)
                {
                    return (_translationProvider.Moderator[language, "INVALID_DESTINATION_TOPIC"], false);
                }

                if (!(postIds?.Any() ?? false))
                {
                    return (_translationProvider.Moderator[language, "ATLEAST_ONE_POST_REQUIRED"], false);
                }

                using var transaction = _sqlExecuter.BeginTransaction(IsolationLevel.Serializable);
                var posts = (await transaction.QueryAsync<PhpbbPosts>(
                    "SELECT * FROM phpbb_posts WHERE post_id IN @postIds ORDER BY post_time", 
                    new { postIds })).AsList();
                if (posts.Count != postIds.Length || posts.Select(p => p.TopicId).Distinct().Count() != 1)
                {
                    return (_translationProvider.Moderator[language, "AT_LEAST_ONE_POST_MOVED_OR_DELETED"], false);
                }

                var newTopic = await transaction.QueryFirstOrDefaultAsync<PhpbbTopics>(
                    "SELECT * FROM phpbb_topics WHERE topic_id = @destinationTopicId", 
                    new { destinationTopicId });
                if (newTopic == null)
                {
                    return (string.Format(_translationProvider.Moderator[language, "TOPIC_DOESNT_EXIST_FORMAT"], destinationTopicId), false);
                }

                await transaction.ExecuteAsync(
                    "UPDATE phpbb_posts SET topic_id = @topicId, forum_id = @forumId WHERE post_id IN @postIds", 
                    new { newTopic.TopicId, newTopic.ForumId, postIds });

                var oldTopicId = posts[0].TopicId;
                var oldForumId = posts[0].ForumId;

				await _postService.CascadePostDelete(transaction, ignoreUser: true, ignoreForums: oldForumId == newTopic.ForumId, ignoreTopics: false, ignoreAttachmentsAndReports: true, posts);
				foreach (var post in posts)
                {
                    post.TopicId = newTopic.TopicId;
                    post.ForumId = newTopic.ForumId;
                    await _operationLogService.LogModeratorPostAction(ModeratorPostActions.MoveSelectedPosts, logDto.UserId, post.PostId, $"Moved from {oldTopicId} to {destinationTopicId}", transaction);
                }
				await _postService.CascadePostAdd(transaction, ignoreUser: true, ignoreForums: oldForumId == newTopic.ForumId, posts);

				await transaction.CommitTransaction();

                return (_translationProvider.Moderator[language, "POSTS_MOVED_SUCCESSFULLY"], true);
            }
            catch (Exception ex)
            {
                var id = _logger.ErrorWithId(ex);
                return (string.Format(_translationProvider.Errors[language, "AN_ERROR_OCCURRED_TRY_AGAIN_ID_FORMAT"], id), false);
            }
        }

        public async Task<(string Message, bool? IsSuccess)> DeletePosts(int[] postIds, OperationLogDto logDto)
        {
            var language = _translationProvider.GetLanguage();
            try
            {
                if (!(postIds?.Any() ?? false))
                {
                    return (_translationProvider.Moderator[language, "ATLEAST_ONE_POST_REQUIRED"], false);
                }

                using var transaction = _sqlExecuter.BeginTransaction(IsolationLevel.Serializable);
                var posts = (await transaction.QueryAsync<PhpbbPosts>("SELECT * FROM phpbb_posts WHERE post_id IN @postIds ORDER BY post_time", new { postIds })).AsList();
                if (posts.Count != postIds.Length || posts.Select(p => p.TopicId).Distinct().Count() != 1)
                {
                    return (_translationProvider.Moderator[language, "ATLEAST_ONE_POST_MOVED_OR_DELETED"], false);
                }

                await DeletePostsCore(posts, logDto, shouldLog: true, ignoreTopics: false, transaction);

                await transaction.CommitTransaction();

                return (_translationProvider.Moderator[language, "POSTS_DELETED_SUCCESSFULLY"], true);
            }
            catch (Exception ex)
            {
                var id = _logger.ErrorWithId(ex);
                return (string.Format(_translationProvider.Errors[language, "AN_ERROR_OCCURRED_TRY_AGAIN_ID_FORMAT"], id), false);
            }
        }

        private async Task DeletePostsCore(List<PhpbbPosts> posts, OperationLogDto logDto, bool shouldLog, bool ignoreTopics, ITransactionalSqlExecuter transaction)
        {
            if (posts.Count == 0)
            {
                return;
            }

            var language = _translationProvider.GetLanguage();
            var postIds = posts.Select(p => p.PostId).DefaultIfEmpty();
            var attachments = (await transaction.QueryAsync<PhpbbAttachments>(
                "SELECT * FROM phpbb_attachments WHERE post_msg_id IN @postIds", 
                new { postIds })).AsList();
            await transaction.ExecuteAsync(
                @"DELETE FROM phpbb_posts WHERE post_id IN @postIds;
                  DELETE FROM phpbb_attachments WHERE post_msg_id IN @postIds", 
                new { postIds });

            foreach (var post in posts)
            {
                var postUserName = post.PostUsername;
                if (string.IsNullOrWhiteSpace(postUserName) && post.PosterId != Constants.ANONYMOUS_USER_ID)
                {
                    var candidate = await transaction.QueryFirstOrDefaultAsync<string>(
                        "SELECT username FROM phpbb_users WHERE user_id = @posterId",
                        new { post.PosterId });
                    if (string.IsNullOrWhiteSpace(candidate))
                    {
						postUserName = _translationProvider.BasicText[language, "ANONYMOUS"];
					}
                    else
                    {
                        postUserName = candidate;
                    }
                }
                else if (post.PosterId == Constants.ANONYMOUS_USER_ID)
                {
                    postUserName = _translationProvider.BasicText[language, "ANONYMOUS"];
				}

                var dto = new PostDto
                {
                    Attachments = attachments.Where(a => a.PostMsgId == post.PostId).Select(a => new AttachmentDto(dbRecord: a, forumId: post.ForumId, isPreview: false, language: language, deletedFile: true)).ToList(),
                    AuthorId = post.PosterId,
                    AuthorName = postUserName,
                    BbcodeUid = post.BbcodeUid,
                    ForumId = post.ForumId,
                    TopicId = post.TopicId,
                    PostId = post.PostId,
                    PostTime = post.PostTime,
                    PostSubject = post.PostSubject,
                    PostText = post.PostText,
                };

                await transaction.ExecuteAsync(
                    "INSERT INTO phpbb_recycle_bin(type, id, content, delete_time, delete_user) VALUES (@type, @id, @content, @now, @userId)",
                    new
                    {
                        type = RecycleBinItemType.Post,
                        id = post.PostId,
                        content = await CompressionUtility.CompressObject(dto),
                        now = DateTime.UtcNow.ToUnixTimestamp(),
                        logDto.UserId
                    });

                if (shouldLog)
                {
                    await _operationLogService.LogModeratorPostAction(ModeratorPostActions.DeleteSelectedPosts, logDto.UserId, post, transaction: transaction);
                }
            }
			await _postService.CascadePostDelete(transaction, ignoreUser: false, ignoreForums: false, ignoreTopics, ignoreAttachmentsAndReports: false, posts);
		}

		public async Task<(string Message, bool? IsSuccess)> DuplicatePost(int postId, OperationLogDto logDto)
        {
            var language = _translationProvider.GetLanguage();
            try
            {
                using var transaction = _sqlExecuter.BeginTransaction(IsolationLevel.Serializable);
                var post = await transaction.QueryFirstOrDefaultAsync<PhpbbPosts>(
                    "SELECT * FROM phpbb_posts WHERE post_id = @postId",
                    new { postId });
                if (post == null)
                {
                    return (_translationProvider.Moderator[language, "ATLEAST_ONE_POST_REQUIRED"], false);
                }
                var attachments = await transaction.QueryAsync<PhpbbAttachments>
                    ("SELECT * FROM phpbb_attachments WHERE post_msg_id = @postId ORDER BY attach_id",
                    new { postId });

                post.PostTime++;
                post.PostAttachment = attachments.Any().ToByte();
                post.PostEditCount = 0;
                post.PostEditReason = string.Empty;
                post.PostEditUser = 0;
                post.PostEditTime = 0;

                var entity = await transaction.QuerySingleAsync<PhpbbPosts>(
                    @$"INSERT INTO phpbb_posts(topic_id, forum_id, poster_id, icon_id, poster_ip, post_time, post_approved, post_reported, enable_bbcode, enable_smilies, enable_magic_url, enable_sig, post_username, post_subject, post_text, post_checksum, post_attachment, bbcode_bitfield, bbcode_uid, post_postcount, post_edit_time, post_edit_reason, post_edit_user, post_edit_count, post_edit_locked)
                       VALUES (@TopicId, @ForumId, @PosterId, @IconId, @PosterIp, @PostTime, @PostApproved, @PostReported, @EnableBbcode, @EnableSmilies, @EnableMagicUrl, @EnableSig, @PostUsername, @PostSubject, @PostText, @PostChecksum, @PostAttachment, @BbcodeBitfield, @BbcodeUid, @PostPostcount, @PostEditTime, @PostEditReason, @PostEditUser, @PostEditCount, @PostEditLocked);
                       SELECT * FROM phpbb_posts WHERE post_id = {_sqlExecuter.LastInsertedItemId}",
                    post);

                foreach (var a in attachments)
                {
					var name = await _storageService.DuplicateAttachment(a, post.PosterId);
					if (string.IsNullOrWhiteSpace(name))
					{
                        continue;
					}
					a.PostMsgId = entity.PostId;
					a.PhysicalFilename = name;
                    await transaction.ExecuteAsync(
                        @"INSERT INTO phpbb_attachments (post_msg_id, topic_id, in_message, poster_id, is_orphan, physical_filename, real_filename, download_count, attach_comment, extension, mimetype, filesize, filetime, thumbnail)
                          VALUES (@PostMsgId, @TopicId, @InMessage, @PosterId, @IsOrphan, @PhysicalFilename, @RealFilename, @DownloadCount, @AttachComment, @Extension, @Mimetype, @Filesize, @Filetime, @Thumbnail);",
                        a);
				}

                await _postService.CascadePostAdd(transaction, ignoreUser: false, ignoreForums: false, entity);

                await _operationLogService.LogModeratorPostAction(ModeratorPostActions.DuplicateSelectedPost, logDto.UserId, postId, transaction: transaction);

                await transaction.CommitTransaction();

                return (string.Empty, true);
            }
            catch (Exception ex)
            {
                var id = _logger.ErrorWithId(ex);
                return (string.Format(_translationProvider.Errors[language, "AN_ERROR_OCCURRED_TRY_AGAIN_ID_FORMAT"], id), false);
            }
        }

		#endregion Post

		public async Task<List<ReportDto>> GetReportedMessages(int forumId)
        {
            return (await _sqlExecuter.QueryAsync<ReportDto>(
                @"SELECT r.report_id AS id, 
	                   rr.reason_title, 
	                   rr.reason_description, 
	                   r.report_text AS details, 
	                   r.user_id AS reporter_id, 
	                   u.username AS reporter_username, 
	                   r.post_id,
                       p.topic_id,
                       t.topic_title,
                       p.forum_id,
                       r.report_time,
                       r.report_closed
                  FROM phpbb_reports r
                  JOIN phpbb_reports_reasons rr ON r.reason_id = rr.reason_id
                  JOIN phpbb_users u on r.user_id = u.user_id
                  LEFT JOIN phpbb_posts p ON r.post_id = p.post_id
                  LEFT JOIN phpbb_topics t on p.topic_id = t.topic_id
                 WHERE report_closed = 0 AND (@forumId = 0 OR p.forum_id = @forumId)",
                new { forumId }
            )).AsList();
        }
    }
}
