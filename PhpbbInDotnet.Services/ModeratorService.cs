using Dapper;
using Microsoft.AspNetCore.Http;
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
        private readonly IUserService _userService;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILogger _logger;
        private readonly ITranslationProvider _translationProvider;

        public ModeratorService(ISqlExecuter sqlExecuter, IPostService postService, IStorageService storageService, ITranslationProvider translationProvider,
            IOperationLogService operationLogService, IUserService userService, IHttpContextAccessor httpContextAccessor, ILogger logger)
        {
            _translationProvider = translationProvider;
            _sqlExecuter = sqlExecuter;
            _postService = postService;
            _storageService = storageService;
            _operationLogService = operationLogService;
            _userService = userService;
            _httpContextAccessor = httpContextAccessor;
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
                using var transaction = _sqlExecuter.BeginTransaction();
                var topicRows = await _sqlExecuter.ExecuteAsync(
                    "UPDATE phpbb_topics SET forum_id = @destinationForumId WHERE topic_id = @topicID AND EXISTS(SELECT 1 FROM phpbb_forums WHERE forum_id = @destinationForumId)",
                    new { topicId, destinationForumId },
                    transaction);

                if (topicRows == 0)
                {
                    return (_translationProvider.Moderator[language, "DESTINATION_DOESNT_EXIST"], false);
                }

                var oldPosts = (await _sqlExecuter.QueryAsync<PhpbbPosts>(
                    "SELECT * FROM phpbb_posts WHERE topic_id = @topicId ORDER BY post_time DESC", 
                    new { topicId },
                    transaction)).AsList();
                var oldForumId = oldPosts.FirstOrDefault()?.ForumId ?? 0;
                await _sqlExecuter.ExecuteAsync(
                    "UPDATE phpbb_posts SET forum_id = @destinationForumId WHERE topic_id = @topicId; " +
                    "UPDATE phpbb_topics_track SET forum_id = @destinationForumId WHERE topic_id = @topicId",
                    new { destinationForumId, topicId },
                    transaction);
                foreach (var post in oldPosts)
                {
                    await CascadePostDeleteCore(post, true, true, transaction);
                    post.ForumId = destinationForumId;
                    await CascadePostAddCore(post, true, transaction);
                }
                await _operationLogService.LogModeratorTopicAction(ModeratorTopicActions.MoveTopic, logDto.UserId, topicId, $"Moved from {oldForumId} to {destinationForumId}.");

                transaction.Commit();

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
                using var transaction = _sqlExecuter.BeginTransaction();
                var posts = (await _sqlExecuter.QueryAsync<PhpbbPosts>(
                    "SELECT * FROM phpbb_posts WHERE topic_id = @topicId", 
                    new { topicId }, 
                    transaction)).AsList();

                if (!posts.Any())
                {
                    return (string.Format(_translationProvider.Moderator[language, "TOPIC_DOESNT_EXIST_FORMAT"], topicId), false);
                }

                var topic = await _sqlExecuter.QueryFirstOrDefaultAsync<PhpbbTopics>("SELECT * FROM phpbb_topics WHERE topic_id = @topicId", new { topicId });
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
                    await _sqlExecuter.ExecuteAsync(
                        "INSERT INTO phpbb_recycle_bin(type, id, content, delete_time, delete_user) VALUES (@type, @id, @content, @now, @userId)",
                        new
                        {
                            type = RecycleBinItemType.Topic,
                            id = topic.TopicId,
                            content = await CompressionUtility.CompressObject(dto),
                            now = DateTime.UtcNow.ToUnixTimestamp(),
                            logDto.UserId
                        },
                        transaction);

                    await _sqlExecuter.ExecuteAsync(
                        "DELETE FROM phpbb_topics WHERE topic_id = @topicId; " +
                        "DELETE FROM phpbb_poll_options WHERE topic_id = @topicId",
                        new { topicId },
                        transaction);
                }

                await DeletePostsCore(posts, logDto, false);

                await _operationLogService.LogModeratorTopicAction(ModeratorTopicActions.DeleteTopic, logDto.UserId, topicId);

                transaction.Commit();

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

                using var transaction = _sqlExecuter.BeginTransaction();
                var posts = (await _sqlExecuter.QueryAsync<PhpbbPosts>(
                    "SELECT * FROM phpbb_posts WHERE post_id IN @postIds ORDER BY post_time", 
                    new { postIds },
                    transaction)).AsList();

                if (posts.Count != postIds.Length)
                {
                    return (_translationProvider.Moderator[language, "ATLEAST_ONE_POST_MOVED_OR_DELETED"], false);
                }

                var curTopic = await _sqlExecuter.QueryFirstOrDefaultAsync<PhpbbTopics>(
                    $@"INSERT INTO phpbb_topics (forum_id, topic_title, topic_time) VALUES (@forumId, @title, @time);
                       SELECT * FROM phpbb_topics WHERE topic_id = {_sqlExecuter.LastInsertedItemId};",
                    new 
                    { 
                        forumId = destinationForumId!.Value, 
                        title = posts.First().PostSubject, 
                        time = posts.First().PostTime 
                    }, 
                    transaction);
                var oldTopicId = posts.First().TopicId;

                await _sqlExecuter.ExecuteAsync(
                    "UPDATE phpbb_posts SET topic_id = @topicId, forum_id = @forumId WHERE post_id IN @postIds", 
                    new { curTopic.TopicId, curTopic.ForumId, postIds }, 
                    transaction);

                foreach (var post in posts)
                {
                    await CascadePostDeleteCore(post, false, true, transaction);
                    post.TopicId = curTopic.TopicId;
                    post.ForumId = curTopic.ForumId;
                    await CascadePostAddCore(post, false, transaction);
                    await _operationLogService.LogModeratorPostAction(ModeratorPostActions.SplitSelectedPosts, logDto.UserId, post.PostId, $"Split from topic {oldTopicId} as new topic in forum {destinationForumId}");
                }

                transaction.Commit();

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

                using var transaction = _sqlExecuter.BeginTransaction();
                var posts = (await _sqlExecuter.QueryAsync<PhpbbPosts>(
                    "SELECT * FROM phpbb_posts WHERE post_id IN @postIds ORDER BY post_time", 
                    new { postIds }, 
                    transaction)).AsList();
                if (posts.Count != postIds.Length || posts.Select(p => p.TopicId).Distinct().Count() != 1)
                {
                    return (_translationProvider.Moderator[language, "AT_LEAST_ONE_POST_MOVED_OR_DELETED"], false);
                }

                var newTopic = await _sqlExecuter.QueryFirstOrDefaultAsync<PhpbbTopics>(
                    "SELECT * FROM phpbb_topics WHERE topic_id = @destinationTopicId", 
                    new { destinationTopicId }, 
                    transaction);
                if (newTopic == null)
                {
                    return (string.Format(_translationProvider.Moderator[language, "TOPIC_DOESNT_EXIST_FORMAT"], destinationTopicId), false);
                }

                await _sqlExecuter.ExecuteAsync(
                    "UPDATE phpbb_posts SET topic_id = @topicId, forum_id = @forumId WHERE post_id IN @postIds", 
                    new { newTopic.TopicId, newTopic.ForumId, postIds }, 
                    transaction);

                var oldTopicId = posts.First().TopicId;
                foreach (var post in posts)
                {
                    await CascadePostDeleteCore(post, false, true, transaction);
                    post.TopicId = newTopic.TopicId;
                    post.ForumId = newTopic.ForumId;
                    await CascadePostAddCore(post, false, transaction);
                    await _operationLogService.LogModeratorPostAction(ModeratorPostActions.MoveSelectedPosts, logDto.UserId, post.PostId, $"Moved from {oldTopicId} to {destinationTopicId}");
                }

                transaction.Commit();

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

                var posts = (await _sqlExecuter.QueryAsync<PhpbbPosts>("SELECT * FROM phpbb_posts WHERE post_id IN @postIds ORDER BY post_time", new { postIds })).AsList();
                if (posts.Count != postIds.Length || posts.Select(p => p.TopicId).Distinct().Count() != 1)
                {
                    return (_translationProvider.Moderator[language, "ATLEAST_ONE_POST_MOVED_OR_DELETED"], false);
                }

                await DeletePostsCore(posts, logDto, true);

                return (_translationProvider.Moderator[language, "POSTS_DELETED_SUCCESSFULLY"], true);
            }
            catch (Exception ex)
            {
                var id = _logger.ErrorWithId(ex);
                return (string.Format(_translationProvider.Errors[language, "AN_ERROR_OCCURRED_TRY_AGAIN_ID_FORMAT"], id), false);
            }
        }

        private async Task DeletePostsCore(List<PhpbbPosts> posts, OperationLogDto logDto, bool shouldLog)
        {
            var language = _translationProvider.GetLanguage();
            var postIds = posts.Select(p => p.PostId).DefaultIfEmpty();
            using var transaction = _sqlExecuter.BeginTransaction();
            var attachments = (await _sqlExecuter.QueryAsync<PhpbbAttachments>(
                "SELECT * FROM phpbb_attachments WHERE post_msg_id IN @postIds", 
                new { postIds },
                transaction)).AsList();
            await _sqlExecuter.ExecuteAsync(
                @"DELETE FROM phpbb_posts WHERE post_id IN @postIds;
                  DELETE FROM phpbb_attachments WHERE post_msg_id IN @postIds", 
                new { postIds },
                transaction);

            foreach (var post in posts)
            {
                var dto = new PostDto
                {
                    Attachments = attachments.Where(a => a.PostMsgId == post.PostId).Select(a => new AttachmentDto(dbRecord: a, forumId: post.ForumId, isPreview: false, language: language, deletedFile: true)).ToList(),
                    AuthorId = post.PosterId,
                    AuthorName = string.IsNullOrWhiteSpace(post.PostUsername) ? _translationProvider.BasicText[language, "ANONYMOUS"] : post.PostUsername,
                    BbcodeUid = post.BbcodeUid,
                    ForumId = post.ForumId,
                    TopicId = post.TopicId,
                    PostId = post.PostId,
                    PostTime = post.PostTime,
                    PostSubject = post.PostSubject,
                    PostText = post.PostText,
                };

                await _sqlExecuter.ExecuteAsync(
                    "INSERT INTO phpbb_recycle_bin(type, id, content, delete_time, delete_user) VALUES (@type, @id, @content, @now, @userId)",
                    new
                    {
                        type = RecycleBinItemType.Post,
                        id = post.PostId,
                        content = await CompressionUtility.CompressObject(dto),
                        now = DateTime.UtcNow.ToUnixTimestamp(),
                        logDto.UserId
                    },
                    transaction);

                await CascadePostDeleteCore(post, false, false, transaction);

                if (shouldLog)
                {
                    await _operationLogService.LogModeratorPostAction(ModeratorPostActions.DeleteSelectedPosts, logDto.UserId, post);
                }
            }

            transaction.Commit();
        }

        public async Task<(string Message, bool? IsSuccess)> DuplicatePost(int postId, OperationLogDto logDto)
        {
            var language = _translationProvider.GetLanguage();
            try
            {
                using var transaction = _sqlExecuter.BeginTransaction();
                var post = await _sqlExecuter.QueryFirstOrDefaultAsync<PhpbbPosts>(
                    "SELECT * FROM phpbb_posts WHERE post_id = @postId",
                    new { postId },
                    transaction);
                if (post == null)
                {
                    return (_translationProvider.Moderator[language, "ATLEAST_ONE_POST_REQUIRED"], false);
                }
                var attachments = await _sqlExecuter.QueryAsync<PhpbbAttachments>
                    ("SELECT * FROM phpbb_attachments WHERE post_msg_id = @postId ORDER BY attach_id",
                    new { postId },
                    transaction);

                post.PostTime++;
                post.PostAttachment = attachments.Any().ToByte();
                post.PostEditCount = 0;
                post.PostEditReason = string.Empty;
                post.PostEditUser = 0;
                post.PostEditTime = 0;

                var entity = await _sqlExecuter.QueryFirstOrDefaultAsync<PhpbbPosts>(
                    @$"INSERT INTO phpbb_posts(topic_id, forum_id, poster_id, icon_id, poster_ip, post_time, post_approved, post_reported, enable_bbcode, enable_smilies, enable_magic_url, enable_sig, post_username, post_subject, post_text, post_checksum, post_attachment, bbcode_bitfield, bbcode_uid, post_postcount, post_edit_time, post_edit_reason, post_edit_user, post_edit_count, post_edit_locked)
                       VALUES (@TopicId, @ForumId, @PosterId, @IconId, @PosterIp, @PostTime, @PostApproved, @PostReported, @EnableBbcode, @EnableSmilies, @EnableMagicUrl, @EnableSig, @PostUsername, @PostSubject, @PostText, @PostChecksum, @PostAttachment, @BbcodeBitfield, @BbcodeUid, @PostPostcount, @PostEditTime, @PostEditReason, @PostEditUser, @PostEditCount, @PostEditLocked);
                       SELECT * FROM phpbb_posts WHERE post_id = {_sqlExecuter.LastInsertedItemId}",
                    post, 
                    transaction);

                foreach (var a in attachments)
                {
					var name = await _storageService.DuplicateAttachment(a, post.PosterId);
					if (string.IsNullOrWhiteSpace(name))
					{
                        continue;
					}
					a.PostMsgId = entity.PostId;
					a.PhysicalFilename = name;
                    await _sqlExecuter.ExecuteAsync(
                        @"INSERT INTO phpbb_attachments (post_msg_id, topic_id, in_message, poster_id, is_orphan, physical_filename, real_filename, download_count, attach_comment, extension, mimetype, filesize, filetime, thumbnail)
                          VALUES (@PostMsgId, @TopicId, @InMessage, @PosterId, @IsOrphan, @PhysicalFilename, @RealFilename, @DownloadCount, @AttachComment, @Extension, @Mimetype, @Filesize, @Filetime, @Thumbnail);",
                        a, 
                        transaction);
				}

                await CascadePostAddCore(entity, false, transaction);

                await _operationLogService.LogModeratorPostAction(ModeratorPostActions.DuplicateSelectedPost, logDto.UserId, postId);

                transaction.Commit();

                return (string.Empty, true);
            }
            catch (Exception ex)
            {
                var id = _logger.ErrorWithId(ex);
                return (string.Format(_translationProvider.Errors[language, "AN_ERROR_OCCURRED_TRY_AGAIN_ID_FORMAT"], id), false);
            }
        }

        public Task CascadePostEdit(PhpbbPosts edited)
            => CascadePostEditCore(edited, transaction: null);

        private async Task CascadePostEditCore(PhpbbPosts edited, IDbTransaction? transaction)
        {
            using var multiple = await _sqlExecuter.QueryMultipleAsync(
                "SELECT * FROM phpbb_topics WHERE topic_id = @topicId;" +
                "SELECT * FROM phpbb_forums WHERE forum_id = @forumId",
                new
                {
                    edited.TopicId,
                    edited.ForumId
                },
                transaction);
            var curTopic = await multiple.ReadFirstOrDefaultAsync<PhpbbTopics>();
            var curForum = await multiple.ReadFirstOrDefaultAsync<PhpbbForums>();
            var usr = await _userService.GetForumUserById(edited.PosterId);

            if (curTopic.TopicFirstPostId == edited.PostId)
            {
                await SetTopicFirstPost(curTopic, edited, usr, true, transaction);
            }

            if (curTopic.TopicLastPostId == edited.PostId)
            {
                await SetTopicLastPost(curTopic, edited, usr, transaction);
            }

            if (curForum.ForumLastPostId == edited.PostId)
            {
                await SetForumLastPost(curForum, edited, usr, transaction);
            }
        }

        public Task CascadePostAdd(PhpbbPosts added, bool ignoreTopic)
            => CascadePostAddCore(added, ignoreTopic, transaction: null);

        private async Task CascadePostAddCore(PhpbbPosts added, bool ignoreTopic, IDbTransaction? transaction)
        {
            using var multiple = await _sqlExecuter.QueryMultipleAsync(
                "SELECT * FROM phpbb_topics WHERE topic_id = @topicId;" +
                "SELECT * FROM phpbb_forums WHERE forum_id = @forumId",
                new
                {
                    added.TopicId,
                    added.ForumId
                },
                transaction);
            var curTopic = await multiple.ReadFirstOrDefaultAsync<PhpbbTopics>();
            var curForum = await multiple.ReadFirstOrDefaultAsync<PhpbbForums>();
            var usr = await _userService.GetForumUserById(added.PosterId);

            await SetForumLastPost(curForum, added, usr, transaction);

            if (!ignoreTopic)
            {
                await SetTopicLastPost(curTopic, added, usr, transaction);
                await SetTopicFirstPost(curTopic, added, usr, false, transaction);
            }

            await _sqlExecuter.ExecuteAsync(
                "UPDATE phpbb_topics SET topic_replies = topic_replies + 1, topic_replies_real = topic_replies_real + 1 WHERE topic_id = @topicId; " +
                "UPDATE phpbb_users SET user_posts = user_posts + 1 WHERE user_id = @userId",
                new { curTopic.TopicId, usr.UserId },
                transaction);
        }

        public Task CascadePostDelete(PhpbbPosts deleted, bool ignoreTopic, bool ignoreAttachmentsAndReports)
            => CascadePostDeleteCore(deleted, ignoreTopic, ignoreAttachmentsAndReports, transaction: null);

        private async Task CascadePostDeleteCore(PhpbbPosts deleted, bool ignoreTopic, bool ignoreAttachmentsAndReports, IDbTransaction? transaction)
        {
            using var multiple = await _sqlExecuter.QueryMultipleAsync(
                "SELECT * FROM phpbb_topics WHERE topic_id = @topicId;" +
                "SELECT count(1) FROM phpbb_posts WHERE topic_id = @topicId;",
                new { deleted.TopicId },
                transaction);
            var curTopic = await multiple.ReadFirstOrDefaultAsync<PhpbbTopics>();
            var postCount = await multiple.ReadSingleAsync<long>();

            if (curTopic != null && postCount > 0)
            {
                if (postCount == 1 && curTopic.TopicLastPostId == deleted.PostId && curTopic.TopicFirstPostId == deleted.PostId && !ignoreTopic)
                {
                    await DeleteTopic(deleted.TopicId, new OperationLogDto
                    {
                        Action = ModeratorTopicActions.DeleteTopic,
                        UserId = _httpContextAccessor.HttpContext?.User is not null && IdentityUtility.TryGetUserId(_httpContextAccessor.HttpContext.User, out var id) ? id : 0
                    });
                }
                else
                {
                    if (curTopic.TopicLastPostId == deleted.PostId && !ignoreTopic)
                    {
                        var lastTopicPost = await _sqlExecuter.QueryFirstOrDefaultAsync<PhpbbPosts>(
                            "SELECT * FROM phpbb_posts WHERE topic_id = @curTopicId AND post_id <> @deletedPostId ORDER BY post_time DESC",
                            new
                            {
                                curTopicId = curTopic.TopicId,
                                deletedPostId = deleted.PostId
                            },
                            transaction);

                        if (lastTopicPost != null)
                        {
                            var lastTopicPostUser = await _userService.GetForumUserById(lastTopicPost.PosterId);
                            await SetTopicLastPost(curTopic, lastTopicPost, lastTopicPostUser, transaction, true);
                        }
                    }

                    if (curTopic.TopicFirstPostId == deleted.PostId && !ignoreTopic)
                    {
                        var firstTopicPost = await _sqlExecuter.QueryFirstOrDefaultAsync<PhpbbPosts>(
                            "SELECT * FROM phpbb_posts WHERE topic_id = @curTopicId AND post_id <> @deletedPostId ORDER BY post_time ASC",
                            new
                            {
                                curTopicId = curTopic.TopicId,
                                deletedPostId = deleted.PostId
                            },
                            transaction);
                        if (firstTopicPost != null)
                        {
                            var firstPostUser = await _userService.GetForumUserById(firstTopicPost.PosterId);
                            await SetTopicFirstPost(curTopic, firstTopicPost, firstPostUser, false, transaction, true);
                        }
                    }

                    if (!ignoreTopic)
                    {
                        await _sqlExecuter.ExecuteAsync(
                            "UPDATE phpbb_topics SET topic_replies = GREATEST(topic_replies - 1, 0), topic_replies_real = GREATEST(topic_replies_real - 1, 0) WHERE topic_id = @topicId",
                            new { curTopic.TopicId },
                            transaction);
                    }
                }
            }


            if (!ignoreAttachmentsAndReports)
            {
                await _sqlExecuter.ExecuteAsync(
                    "DELETE FROM phpbb_reports WHERE post_id = @postId; " +
                    "DELETE FROM phpbb_attachments WHERE post_msg_id = @postId",
                    new { deleted.PostId },
                    transaction);
            }

            if (curTopic != null)
            {
                var curForum = await _sqlExecuter.QueryFirstOrDefaultAsync<PhpbbForums>(
                    "SELECT * FROM phpbb_forums WHERE forum_id = @forumId", 
                    new { forumId = curTopic?.ForumId ?? deleted.ForumId },
                    transaction);
                if (curForum != null && curForum.ForumLastPostId == deleted.PostId)
                {
                    var lastForumPost = await _sqlExecuter.QueryFirstOrDefaultAsync<PhpbbPosts>(
                        "SELECT * FROM phpbb_posts WHERE forum_id = @curForumId AND post_id <> @deletedPostId ORDER BY post_time DESC",
                        new
                        {
                            curForumId = curForum.ForumId,
                            deletedPostId = deleted.PostId
                        },
                        transaction);
                    if (lastForumPost != null)
                    {
                        var lastForumPostUser = await _userService.GetForumUserById(lastForumPost.PosterId);
                        await SetForumLastPost(curForum, lastForumPost, lastForumPostUser, transaction, true);
                    }
                }
            }

            await _sqlExecuter.ExecuteAsync(
                "UPDATE phpbb_users SET user_posts = user_posts - 1 WHERE user_id = @posterId",
                new { deleted.PosterId },
                transaction);
        }

        private async Task SetTopicLastPost(PhpbbTopics topic, PhpbbPosts post, ForumUser author, IDbTransaction? transaction, bool hardReset = false)
        {
            if (hardReset || topic.TopicLastPostTime < post.PostTime)
            {
                topic.TopicLastPostId = post.PostId;
                topic.TopicLastPostSubject = post.PostSubject;
                topic.TopicLastPostTime = post.PostTime;
                topic.TopicLastPosterColour = author.UserColor!;
                topic.TopicLastPosterId = post.PosterId;
                topic.TopicLastPosterName = author.UserId == Constants.ANONYMOUS_USER_ID ? post.PostUsername : author.Username!;

                await _sqlExecuter.ExecuteAsync(
                    @"UPDATE phpbb_topics 
                         SET topic_last_post_id = @TopicLastPostId, 
                             topic_last_post_subject = @TopicLastPostSubject, 
                             topic_last_post_time = @TopicLastPostTime, 
                             topic_last_poster_colour = @TopicLastPosterColour, 
                             topic_last_poster_id = @TopicLastPosterId, 
                             topic_last_poster_name = @TopicLastPosterName 
                       WHERE topic_id = @TopicId",
                    topic,
                    transaction);
            }
        }

        private async Task SetForumLastPost(PhpbbForums forum, PhpbbPosts post, ForumUser author, IDbTransaction? transaction, bool hardReset = false)
        {
            if (hardReset || forum.ForumLastPostTime < post.PostTime)
            {
                forum.ForumLastPostId = post.PostId;
                forum.ForumLastPostSubject = post.PostSubject;
                forum.ForumLastPostTime = post.PostTime;
                forum.ForumLastPosterColour = author.UserColor!;
                forum.ForumLastPosterId = post.PosterId;
                forum.ForumLastPosterName = author.UserId == Constants.ANONYMOUS_USER_ID ? post.PostUsername : author.Username!;

                await _sqlExecuter.ExecuteAsync(
                    @"UPDATE phpbb_forums 
                         SET forum_last_post_id = @ForumLastPostId, 
                             forum_last_post_subject = @ForumLastPostSubject, 
                             forum_last_post_time = @ForumLastPostTime, 
                             forum_last_poster_colour = @ForumLastPosterColour, 
                             forum_last_poster_id = @ForumLastPosterId, 
                             forum_last_poster_name = @ForumLastPosterName 
                       WHERE forum_id = @ForumId",
                    forum,
                    transaction);
            }
        }

        private async Task SetTopicFirstPost(PhpbbTopics topic, PhpbbPosts post, ForumUser author, bool setTopicTitle, IDbTransaction? transaction, bool goForward = false)
        {
            var curFirstPost = await _sqlExecuter.QueryFirstOrDefaultAsync<PhpbbPosts>("SELECT * FROM phpbb_posts WHERE post_id = @TopicFirstPostId", new { topic.TopicFirstPostId });
            if (topic.TopicFirstPostId == 0 || goForward || (curFirstPost != null && curFirstPost.PostTime >= post.PostTime))
            {
                if (setTopicTitle)
                {
                    topic.TopicTitle = post.PostSubject.Replace(Constants.REPLY, string.Empty).Trim();
                }
                topic.TopicFirstPostId = post.PostId;
                topic.TopicFirstPosterColour = author.UserColor!;
                topic.TopicFirstPosterName = author.Username!;

                await _sqlExecuter.ExecuteAsync(
                    @"UPDATE phpbb_topics 
                         SET topic_title = @TopicTitle, 
                             topic_first_post_id = @TopicFirstPostId, 
                             topic_first_poster_colour = @TopicFirstPosterColour, 
                             topic_first_poster_name = @TopicFirstPosterName
                    WHERE topic_id = @topicId",
                    topic,
                    transaction);
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
