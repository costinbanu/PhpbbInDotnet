using Dapper;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using PhpbbInDotnet.Database;
using PhpbbInDotnet.Database.Entities;
using PhpbbInDotnet.Languages;
using PhpbbInDotnet.Objects;
using PhpbbInDotnet.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace PhpbbInDotnet.Services
{
    public class ModeratorService : MultilingualServiceBase
    {
        private readonly ForumDbContext _context;
        private readonly PostService _postService;
        private readonly StorageService _storageService;
        private readonly OperationLogService _operationLogService;

        public ModeratorService(ForumDbContext context, PostService postService, StorageService storageService, CommonUtils utils, LanguageProvider languageProvider, 
            IHttpContextAccessor httpContextAccessor, OperationLogService operationLogService)
            : base(utils, languageProvider, httpContextAccessor)
        {
            _context = context;
            _postService = postService;
            _storageService = storageService;
            _operationLogService = operationLogService;
        }

        #region Topic

        public async Task<(string Message, bool? IsSuccess)> ChangeTopicType(int topicId, TopicType topicType, OperationLogDto logDto)
        {
            try
            {
                var conn = _context.GetDbConnection();
                var rows = await conn.ExecuteAsync("UPDATE phpbb_topics SET topic_type = @topicType WHERE topic_id = @topicId", new { topicType, topicId });

                if (rows == 1)
                {
                    await _operationLogService.LogModeratorTopicAction((ModeratorTopicActions)logDto.Action!, logDto.UserId, topicId);
                    return (LanguageProvider.Moderator[GetLanguage(), "TOPIC_CHANGED_SUCCESSFULLY"], true);
                }
                else
                {
                    return (string.Format(LanguageProvider.Moderator[GetLanguage(), "TOPIC_DOESNT_EXIST_FORMAT"], topicId), false);
                }

            }
            catch (Exception ex)
            {
                var id = Utils.HandleError(ex);
                return (string.Format(LanguageProvider.Errors[GetLanguage(), "AN_ERROR_OCCURRED_TRY_AGAIN_ID_FORMAT"], id), false);
            }
        }

        public async Task<(string Message, bool? IsSuccess)> MoveTopic(int topicId, int destinationForumId, OperationLogDto logDto)
        {
            try
            {
                var conn = _context.GetDbConnection();
                
                var topicRows = await conn.ExecuteAsync(
                    "UPDATE phpbb_topics SET forum_id = @destinationForumId WHERE topic_id = @topicID AND EXISTS(SELECT 1 FROM phpbb_forums WHERE forum_id = @destinationForumId)",
                    new { topicId, destinationForumId }
                );

                if (topicRows == 0)
                {
                    return (LanguageProvider.Moderator[GetLanguage(), "DESTINATION_DOESNT_EXIST"], false);
                }

                var oldPosts = (await conn.QueryAsync<PhpbbPosts>("SELECT * FROM phpbb_posts WHERE topic_id = @topicId ORDER BY post_time DESC", new { topicId })).AsList();
                var oldForumId = oldPosts.FirstOrDefault()?.ForumId ?? 0;
                await conn.ExecuteAsync(
                    "UPDATE phpbb_posts SET forum_id = @destinationForumId WHERE topic_id = @topicId; " +
                    "UPDATE phpbb_topics_track SET forum_id = @destinationForumId WHERE topic_id = @topicId", 
                    new { destinationForumId, topicId }
                );
                foreach (var post in oldPosts)
                {
                    await _postService.CascadePostDelete(post, true, true);
                    post.ForumId = destinationForumId;
                    await _postService.CascadePostAdd(post, true);
                }
                await _operationLogService.LogModeratorTopicAction(ModeratorTopicActions.MoveTopic, logDto.UserId, topicId, $"Moved from {oldForumId} to {destinationForumId}.");

                return (LanguageProvider.Moderator[GetLanguage(), "TOPIC_CHANGED_SUCCESSFULLY"], true);
            }
            catch (Exception ex)
            {
                var id = Utils.HandleError(ex);
                return (string.Format(LanguageProvider.Errors[GetLanguage(), "AN_ERROR_OCCURRED_TRY_AGAIN_ID_FORMAT"], id), false);
            }
        }

        public async Task<(string Message, bool? IsSuccess)> LockUnlockTopic(int topicId, bool @lock, OperationLogDto logDto)
        {
            try
            {
                var conn = _context.GetDbConnection();
                
                var rows = await conn.ExecuteAsync("UPDATE phpbb_topics SET topic_status = @status WHERE topic_id = @topicId", new { status = @lock.ToByte(), topicId });

                if (rows == 0)
                {
                    return (string.Format(LanguageProvider.Moderator[GetLanguage(), "TOPIC_DOESNT_EXIST_FORMAT"], topicId), false);
                }
                await _operationLogService.LogModeratorTopicAction((ModeratorTopicActions)logDto.Action!, logDto.UserId, topicId);

                return (LanguageProvider.Moderator[GetLanguage(), "TOPIC_CHANGED_SUCCESSFULLY"], true);
            }
            catch (Exception ex)
            {
                var id = Utils.HandleError(ex);
                return (string.Format(LanguageProvider.Errors[GetLanguage(), "AN_ERROR_OCCURRED_TRY_AGAIN_ID_FORMAT"], id), false);
            }
        }
        
        public async Task<(string Message, bool? IsSuccess)> DeleteTopic(int topicId, OperationLogDto logDto)
        {
            try
            {
                var conn = _context.GetDbConnection();
                var posts = (await conn.QueryAsync<PhpbbPosts>("SELECT * FROM phpbb_posts WHERE topic_id = @topicId", new { topicId })).AsList();
                if (!posts.Any())
                {
                    return (string.Format(LanguageProvider.Moderator[GetLanguage(), "TOPIC_DOESNT_EXIST_FORMAT"], topicId), false);
                }

                var topic = await conn.QueryFirstOrDefaultAsync<PhpbbTopics>("SELECT * FROM phpbb_topics WHERE topic_id = @topicId", new { topicId });
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
                    await conn.ExecuteAsync(
                        "INSERT INTO phpbb_recycle_bin(type, id, content, delete_time, delete_user) VALUES (@type, @id, @content, @now, @userId)",
                        new
                        {
                            type = RecycleBinItemType.Topic,
                            id = topic.TopicId,
                            content = await Utils.CompressObject(dto),
                            now = DateTime.UtcNow.ToUnixTimestamp(),
                            logDto.UserId
                        }
                    );
                    await conn.ExecuteAsync(
                        "DELETE FROM phpbb_topics WHERE topic_id = @topicId; " +
                        "DELETE FROM phpbb_poll_options WHERE topic_id = @topicId", 
                        new { topicId }
                    );
                }

                await DeletePostsCore(posts, logDto, false);

                await _operationLogService.LogModeratorTopicAction(ModeratorTopicActions.DeleteTopic, logDto.UserId, topicId);

                return (LanguageProvider.Moderator[GetLanguage(), "TOPIC_DELETED_SUCCESSFULLY"], true);
            }
            catch (Exception ex)
            {
                var id = Utils.HandleError(ex);
                return (string.Format(LanguageProvider.Errors[GetLanguage(), "AN_ERROR_OCCURRED_TRY_AGAIN_ID_FORMAT"], id), false);
            }
        }

        public async Task<(string Message, bool? IsSuccess)> CreateShortcut(int topicId, int forumId, OperationLogDto logDto)
        {
            var lang = GetLanguage();
            try
            {
                var conn = _context.GetDbConnection();
                var curTopic = await conn.QueryFirstOrDefaultAsync<PhpbbTopics>(
                    "SELECT * FROM phpbb_topics WHERE topic_id = @topicId", 
                    new { topicId });

                if (curTopic is null)
                {
                    return (string.Format(LanguageProvider.Moderator[lang, "TOPIC_DOESNT_EXIST_FORMAT"], topicId), false);
                }
                
                if (curTopic.ForumId == forumId)
                {
                    return (LanguageProvider.Moderator[lang, "INVALID_DESTINATION_FORUM"], false);
                }

                await _context.GetDbConnection().ExecuteAsync(
                    "INSERT INTO phpbb_shortcuts (topic_id, forum_id) VALUES(@topicId, @forumId)", 
                    new { topicId, forumId });

                await _operationLogService.LogModeratorTopicAction(ModeratorTopicActions.CreateShortcut, logDto.UserId, topicId);

                return (LanguageProvider.Moderator[lang, "SHORTCUT_CREATED_SUCCESSFULLY"], true);
            }
            catch (Exception ex)
            {
                var id = Utils.HandleError(ex);
                return (string.Format(LanguageProvider.Errors[lang, "AN_ERROR_OCCURRED_TRY_AGAIN_ID_FORMAT"], id), false);
            }
        }

        public async Task<(string Message, bool? IsSuccess)> RemoveShortcut(int topicId, int forumId, OperationLogDto logDto)
        {
            var lang = GetLanguage();
            try
            {
                var conn = _context.GetDbConnection();
                var curShortcut = await conn.QueryFirstOrDefaultAsync<PhpbbShortcuts>(
                    "SELECT * FROM phpbb_shortcuts WHERE topic_id = @topicId",
                    new { topicId });

                if (curShortcut is null)
                {
                    return (string.Format(LanguageProvider.Moderator[lang, "SHORTCUT_DOESNT_EXIST_FORMAT"], topicId, forumId), false);
                }

                if (curShortcut.ForumId != forumId)
                {
                    return (LanguageProvider.Moderator[lang, "INVALID_SHORTCUT_SELECTED"], false);
                }

                await _context.GetDbConnection().ExecuteAsync(
                    "DELETE FROM phpbb_shortcuts WHERE topic_id = @topicId AND forum_id = @forumId",
                    new { topicId, forumId });

                await _operationLogService.LogModeratorTopicAction(ModeratorTopicActions.CreateShortcut, logDto.UserId, topicId);

                return (LanguageProvider.Moderator[lang, "SHORTCUT_DELETED_SUCCESSFULLY"], true);
            }
            catch (Exception ex)
            {
                var id = Utils.HandleError(ex);
                return (string.Format(LanguageProvider.Errors[lang, "AN_ERROR_OCCURRED_TRY_AGAIN_ID_FORMAT"], id), false);
            }
        }

        #endregion Topic

        #region Post

        public async Task<(string Message, bool? IsSuccess)> SplitPosts(int[] postIds, int? destinationForumId, OperationLogDto logDto)
        {
            try
            {
                if ((destinationForumId ?? 0) == 0)
                {
                    return (LanguageProvider.Moderator[GetLanguage(), "INVALID_DESTINATION_FORUM"], false); 
                }

                if (!(postIds?.Any() ?? false))
                {
                    return (LanguageProvider.Moderator[GetLanguage(), "ATLEAST_ONE_POST_REQUIRED"], false);
                }

                var conn = _context.GetDbConnection();

                var posts = (await conn.QueryAsync<PhpbbPosts>("SELECT * FROM phpbb_posts WHERE post_id IN @postIds ORDER BY post_time", new { postIds })).AsList();

                if (posts.Count != postIds.Length)
                {
                    return (LanguageProvider.Moderator[GetLanguage(), "ATLEAST_ONE_POST_MOVED_OR_DELETED"], false);
                }

                var curTopic = await conn.QueryFirstOrDefaultAsync<PhpbbTopics>(
                    "INSERT INTO phpbb_topics (forum_id, topic_title, topic_time) VALUES (@forumId, @title, @time); " +
                    "SELECT * FROM phpbb_topics WHERE topic_id = LAST_INSERT_ID();",
                    new { forumId = destinationForumId!.Value, title = posts.First().PostSubject, time = posts.First().PostTime }
                );
                var oldTopicId = posts.First().TopicId;

                await conn.ExecuteAsync("UPDATE phpbb_posts SET topic_id = @topicId, forum_id = @forumId WHERE post_id IN @postIds", new { curTopic.TopicId, curTopic.ForumId, postIds });

                foreach (var post in posts)
                {
                    await _postService.CascadePostDelete(post, false, true);
                    post.TopicId = curTopic.TopicId;
                    post.ForumId = curTopic.ForumId;
                    await _postService.CascadePostAdd(post, false);
                    await _operationLogService.LogModeratorPostAction(ModeratorPostActions.SplitSelectedPosts, logDto.UserId, post.PostId, $"Split from topic {oldTopicId} as new topic in forum {destinationForumId}");
                }

                return (LanguageProvider.Moderator[GetLanguage(), "POSTS_SPLIT_SUCCESSFULLY"], true);
            }
            catch (Exception ex)
            {
                var id = Utils.HandleError(ex);
                return (string.Format(LanguageProvider.Errors[GetLanguage(), "AN_ERROR_OCCURRED_TRY_AGAIN_ID_FORMAT"], id), false);
            }
        }

        public async Task<(string Message, bool? IsSuccess)> MovePosts(int[] postIds, int? destinationTopicId, OperationLogDto logDto)
        {
            try
            {
                if ((destinationTopicId ?? 0) == 0)
                {
                    return (LanguageProvider.Moderator[GetLanguage(), "INVALID_DESTINATION_TOPIC"], false);
                }

                if (!(postIds?.Any() ?? false))
                {
                    return (LanguageProvider.Moderator[GetLanguage(), "ATLEAST_ONE_POST_REQUIRED"], false);
                }

                var conn = _context.GetDbConnection();

                var posts = (await conn.QueryAsync<PhpbbPosts>("SELECT * FROM phpbb_posts WHERE post_id IN @postIds ORDER BY post_time", new { postIds })).AsList();
                if (posts.Count != postIds.Length || posts.Select(p => p.TopicId).Distinct().Count() != 1)
                {
                    return (LanguageProvider.Moderator[GetLanguage(), "AT_LEAST_ONE_POST_MOVED_OR_DELETED"], false);
                }

                var newTopic = await conn.QueryFirstOrDefaultAsync<PhpbbTopics>("SELECT * FROM phpbb_topics WHERE topic_id = @destinationTopicId", new { destinationTopicId });
                if (newTopic == null)
                {
                    return (string.Format(LanguageProvider.Moderator[GetLanguage(), "TOPIC_DOESNT_EXIST_FORMAT"], destinationTopicId), false);
                }

                await conn.ExecuteAsync("UPDATE phpbb_posts SET topic_id = @topicId, forum_id = @forumId WHERE post_id IN @postIds", new { newTopic.TopicId, newTopic.ForumId, postIds });

                var oldTopicId = posts.First().TopicId;
                foreach (var post in posts)
                {
                    await _postService.CascadePostDelete(post, false, true);
                    post.TopicId = newTopic.TopicId;
                    post.ForumId = newTopic.ForumId;
                    await _postService.CascadePostAdd(post, false);
                    await _operationLogService.LogModeratorPostAction(ModeratorPostActions.MoveSelectedPosts, logDto.UserId, post.PostId, $"Moved from {oldTopicId} to {destinationTopicId}");
                }

                return (LanguageProvider.Moderator[GetLanguage(), "POSTS_MOVED_SUCCESSFULLY"], true);
            }
            catch (Exception ex)
            {
                var id = Utils.HandleError(ex);
                return (string.Format(LanguageProvider.Errors[GetLanguage(), "AN_ERROR_OCCURRED_TRY_AGAIN_ID_FORMAT"], id), false);
            }
        }

        public async Task<(string Message, bool? IsSuccess)> DeletePosts(int[] postIds, OperationLogDto logDto)
        {
            try
            {
                var lang = GetLanguage();
                if (!(postIds?.Any() ?? false))
                {
                    return (LanguageProvider.Moderator[lang, "ATLEAST_ONE_POST_REQUIRED"], false);
                }

                var conn = _context.GetDbConnection();
                var posts = (await conn.QueryAsync<PhpbbPosts>("SELECT * FROM phpbb_posts WHERE post_id IN @postIds ORDER BY post_time", new { postIds })).AsList();
                if (posts.Count != postIds.Length || posts.Select(p => p.TopicId).Distinct().Count() != 1)
                {
                    return (LanguageProvider.Moderator[lang, "ATLEAST_ONE_POST_MOVED_OR_DELETED"], false);
                }

                await DeletePostsCore(posts, logDto, true);

                return (LanguageProvider.Moderator[lang, "POSTS_DELETED_SUCCESSFULLY"], true);
            }
            catch (Exception ex)
            {
                var id = Utils.HandleError(ex);
                return (string.Format(LanguageProvider.Errors[GetLanguage(), "AN_ERROR_OCCURRED_TRY_AGAIN_ID_FORMAT"], id), false);
            }
        }

        private async Task DeletePostsCore(List<PhpbbPosts> posts, OperationLogDto logDto, bool shouldLog)
        {
            var lang = GetLanguage();
            var conn = _context.GetDbConnection();
            var postIds = posts.Select(p => p.PostId).ToList();
            var attachments = (await conn.QueryAsync<PhpbbAttachments>("SELECT * FROM phpbb_attachments WHERE post_msg_id IN @postIds", new { postIds })).AsList();
            await Task.WhenAll(
                conn.ExecuteAsync("DELETE FROM phpbb_posts WHERE post_id IN @postIds", new { postIds }),
                conn.ExecuteAsync("DELETE FROM phpbb_attachments WHERE post_msg_id IN @postIds", new { postIds })
            );
            foreach (var post in posts)
            {
                var dto = new PostDto
                {
                    Attachments = attachments.Where(a => a.PostMsgId == post.PostId).Select(a => new AttachmentDto(dbRecord: a, isPreview: false, language: lang, deletedFile: true)).ToList(),
                    AuthorId = post.PosterId,
                    AuthorName = string.IsNullOrWhiteSpace(post.PostUsername) ? LanguageProvider.BasicText[lang, "ANONYMOUS"] : post.PostUsername,
                    BbcodeUid = post.BbcodeUid,
                    ForumId = post.ForumId,
                    TopicId = post.TopicId,
                    PostId = post.PostId,
                    PostTime = post.PostTime,
                    PostSubject = post.PostSubject,
                    PostText = post.PostText,
                };

                await conn.ExecuteAsync(
                    "INSERT INTO phpbb_recycle_bin(type, id, content, delete_time, delete_user) VALUES (@type, @id, @content, @now, @userId)",
                    new
                    {
                        type = RecycleBinItemType.Post,
                        id = post.PostId,
                        content = await Utils.CompressObject(dto),
                        now = DateTime.UtcNow.ToUnixTimestamp(),
                        logDto.UserId
                    }
                );
                await _postService.CascadePostDelete(post, false, false);
                if (shouldLog)
                {
                    await _operationLogService.LogModeratorPostAction(ModeratorPostActions.DeleteSelectedPosts, logDto.UserId, post);
                }
            }
        }

        public async Task<(string Message, bool? IsSuccess)> DuplicatePost(int postId, OperationLogDto logDto)
        {
            try
            {
                var post = await _context.PhpbbPosts.AsNoTracking().FirstOrDefaultAsync(p => p.PostId == postId);
                if (post == null)
                {
                    return (LanguageProvider.Moderator[GetLanguage(), "ATLEAST_ONE_POST_REQUIRED"], false);
                }
                var attachments = await _context.PhpbbAttachments.AsNoTracking().Where(a => a.PostMsgId == postId).OrderBy(a => a.AttachId).ToListAsync();

                post.PostId = 0;
                post.PostTime++;
                post.PostAttachment = attachments.Any().ToByte();
                post.PostEditCount = 0;
                post.PostEditReason = string.Empty;
                post.PostEditUser = 0;
                post.PostEditTime = 0;
                var entity = await _context.PhpbbPosts.AddAsync(post);

                entity.Entity.PostId = 0;
                await _context.SaveChangesAsync();

                if (attachments.Any())
                {
                    await _context.PhpbbAttachments.AddRangeAsync(
                        attachments.Select(a =>
                        {
                            var name = _storageService.DuplicateFile(a, post.PosterId);
                            if (string.IsNullOrWhiteSpace(name))
                            {
                                return null;
                            }
                            a.AttachId = 0;
                            a.PostMsgId = entity.Entity.PostId;
                            a.PhysicalFilename = name;
                            return a;
                        }).Where(a => a != null)!);

                    await _context.SaveChangesAsync();
                }

                await _postService.CascadePostAdd(entity.Entity, false);

                await _operationLogService.LogModeratorPostAction(ModeratorPostActions.DuplicateSelectedPost, logDto.UserId, postId);

                return (string.Empty, true);
            }
            catch (Exception ex)
            {
                var id = Utils.HandleError(ex);
                return (string.Format(LanguageProvider.Errors[GetLanguage(), "AN_ERROR_OCCURRED_TRY_AGAIN_ID_FORMAT"], id), false);
            }
        }

        #endregion Post

        public async Task<List<ReportDto>> GetReportedMessages(int forumId)
        {
            var connection = _context.GetDbConnection();
            return (await connection.QueryAsync<ReportDto>(
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
