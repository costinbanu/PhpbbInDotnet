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
                var conn = _context.Database.GetDbConnection();
                var rows = await conn.ExecuteAsync("UPDATE phpbb_topics SET topic_type = @topicType WHERE topic_id = @topicId", new { topicType, topicId });

                if (rows == 1)
                {
                    await _operationLogService.LogModeratorTopicAction((ModeratorTopicActions)logDto.Action, logDto.UserId, topicId);
                    return (LanguageProvider.Moderator[await GetLanguage(), "TOPIC_CHANGED_SUCCESSFULLY"], true);
                }
                else
                {
                    return (string.Format(LanguageProvider.Moderator[await GetLanguage(), "TOPIC_DOESNT_EXIST_FORMAT"], topicId), false);
                }

            }
            catch (Exception ex)
            {
                var id = Utils.HandleError(ex);
                return (string.Format(LanguageProvider.Errors[await GetLanguage(), "AN_ERROR_OCCURRED_TRY_AGAIN_ID_FORMAT"], id), false);
            }
        }

        public async Task<(string Message, bool? IsSuccess)> MoveTopic(int topicId, int destinationForumId, OperationLogDto logDto)
        {
            try
            {
                var conn = _context.Database.GetDbConnection();
                
                var topicRows = await conn.ExecuteAsync(
                    "UPDATE phpbb_topics SET forum_id = @destinationForumId WHERE topic_id = @topicID AND EXISTS(SELECT 1 FROM phpbb_forums WHERE forum_id = @destinationForumId)",
                    new { topicId, destinationForumId }
                );

                if (topicRows == 0)
                {
                    return (LanguageProvider.Moderator[await GetLanguage(), "DESTINATION_DOESNT_EXIST"], false);
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
                    await _postService.CascadePostDelete(post, true);
                    post.ForumId = destinationForumId;
                    await _postService.CascadePostAdd(post, true);
                }
                await _operationLogService.LogModeratorTopicAction(ModeratorTopicActions.MoveTopic, logDto.UserId, topicId, $"Moved from {oldForumId} to {destinationForumId}.");

                return (LanguageProvider.Moderator[await GetLanguage(), "TOPIC_CHANGED_SUCCESSFULLY"], true);
            }
            catch (Exception ex)
            {
                var id = Utils.HandleError(ex);
                return (string.Format(LanguageProvider.Errors[await GetLanguage(), "AN_ERROR_OCCURRED_TRY_AGAIN_ID_FORMAT"], id), false);
            }
        }

        public async Task<(string Message, bool? IsSuccess)> LockUnlockTopic(int topicId, bool @lock, OperationLogDto logDto)
        {
            try
            {
                var conn = _context.Database.GetDbConnection();
                
                var rows = await conn.ExecuteAsync("UPDATE phpbb_topics SET topic_status = @status WHERE topic_id = @topicId", new { status = @lock.ToByte(), topicId });

                if (rows == 0)
                {
                    return (string.Format(LanguageProvider.Moderator[await GetLanguage(), "TOPIC_DOESNT_EXIST_FORMAT"], topicId), false);
                }
                await _operationLogService.LogModeratorTopicAction((ModeratorTopicActions)logDto.Action, logDto.UserId, topicId);

                return (LanguageProvider.Moderator[await GetLanguage(), "TOPIC_CHANGED_SUCCESSFULLY"], true);
            }
            catch (Exception ex)
            {
                var id = Utils.HandleError(ex);
                return (string.Format(LanguageProvider.Errors[await GetLanguage(), "AN_ERROR_OCCURRED_TRY_AGAIN_ID_FORMAT"], id), false);
            }
        }
        
        public async Task<(string Message, bool? IsSuccess)> DeleteTopic(int topicId, OperationLogDto logDto)
        {
            try
            {
                var conn = _context.Database.GetDbConnection();
                
                var posts = (await conn.QueryAsync<PhpbbPosts>("SELECT * FROM phpbb_posts WHERE topic_id = @topicId", new { topicId })).AsList();
                
                if (!posts.Any())
                {
                    return (string.Format(LanguageProvider.Moderator[await GetLanguage(), "TOPIC_DOESNT_EXIST_FORMAT"], topicId), false);
                }

                await conn.ExecuteAsync("DELETE FROM phpbb_posts WHERE topic_id = @topicId", new { topicId });
                posts.ForEach(async (p) => await _postService.CascadePostDelete(p, false));

                await _operationLogService.LogModeratorTopicAction(ModeratorTopicActions.DeleteTopic, logDto.UserId, topicId);

                return (LanguageProvider.Moderator[await GetLanguage(), "TOPIC_DELETED_SUCCESSFULLY"], true);
            }
            catch (Exception ex)
            {
                var id = Utils.HandleError(ex);
                return (string.Format(LanguageProvider.Errors[await GetLanguage(), "AN_ERROR_OCCURRED_TRY_AGAIN_ID_FORMAT"], id), false);
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
                    return (LanguageProvider.Moderator[await GetLanguage(), "INVALID_DESTINATION_FORUM"], false); 
                }

                if (!(postIds?.Any() ?? false))
                {
                    return (LanguageProvider.Moderator[await GetLanguage(), "ATLEAST_ONE_POST_REQUIRED"], false);
                }

                var conn = _context.Database.GetDbConnection();

                var posts = (await conn.QueryAsync<PhpbbPosts>("SELECT * FROM phpbb_posts WHERE post_id IN @postIds ORDER BY post_time", new { postIds })).AsList();

                if (posts.Count != postIds.Length)
                {
                    return (LanguageProvider.Moderator[await GetLanguage(), "ATLEAST_ONE_POST_MOVED_OR_DELETED"], false);
                }

                var curTopic = await conn.QueryFirstOrDefaultAsync<PhpbbTopics>(
                    "INSERT INTO phpbb_topics (forum_id, topic_title, topic_time) VALUES (@forumId, @title, @time); " +
                    "SELECT * FROM phpbb_topics WHERE topic_id = LAST_INSERT_ID();",
                    new { forumId = destinationForumId.Value, title = posts.First().PostSubject, time = posts.First().PostTime }
                );
                var oldTopicId = posts.First().TopicId;

                await conn.ExecuteAsync("UPDATE phpbb_posts SET topic_id = @topicId, forum_id = @forumId WHERE post_id IN @postIds", new { curTopic.TopicId, curTopic.ForumId, postIds });

                foreach (var post in posts)
                {
                    await _postService.CascadePostDelete(post, false);
                    post.TopicId = curTopic.TopicId;
                    post.ForumId = curTopic.ForumId;
                    await _postService.CascadePostAdd(post, false);
                    await _operationLogService.LogModeratorPostAction(ModeratorPostActions.SplitSelectedPosts, logDto.UserId, post.PostId, $"Split from topic {oldTopicId} as new topic in forum {destinationForumId}");
                }

                return (LanguageProvider.Moderator[await GetLanguage(), "POSTS_SPLIT_SUCCESSFULLY"], true);
            }
            catch (Exception ex)
            {
                var id = Utils.HandleError(ex);
                return (string.Format(LanguageProvider.Errors[await GetLanguage(), "AN_ERROR_OCCURRED_TRY_AGAIN_ID_FORMAT"], id), false);
            }
        }

        public async Task<(string Message, bool? IsSuccess)> MovePosts(int[] postIds, int? destinationTopicId, OperationLogDto logDto)
        {
            try
            {
                if ((destinationTopicId ?? 0) == 0)
                {
                    return (LanguageProvider.Moderator[await GetLanguage(), "INVALID_DESTINATION_TOPIC"], false);
                }

                if (!(postIds?.Any() ?? false))
                {
                    return (LanguageProvider.Moderator[await GetLanguage(), "ATLEAST_ONE_POST_REQUIRED"], false);
                }

                var conn = _context.Database.GetDbConnection();

                var posts = (await conn.QueryAsync<PhpbbPosts>("SELECT * FROM phpbb_posts WHERE post_id IN @postIds ORDER BY post_time", new { postIds })).AsList();
                if (posts.Count != postIds.Length || posts.Select(p => p.TopicId).Distinct().Count() != 1)
                {
                    return (LanguageProvider.Moderator[await GetLanguage(), "AT_LEAST_ONE_POST_MOVED_OR_DELETED"], false);
                }

                var newTopic = await conn.QueryFirstOrDefaultAsync<PhpbbTopics>("SELECT * FROM phpbb_topics WHERE topic_id = @destinationTopicId", new { destinationTopicId });
                if (newTopic == null)
                {
                    return (string.Format(LanguageProvider.Moderator[await GetLanguage(), "TOPIC_DOESNT_EXIST_FORMAT"], destinationTopicId), false);
                }

                await conn.ExecuteAsync("UPDATE phpbb_posts SET topic_id = @topicId, forum_id = @forumId WHERE post_id IN @postIds", new { newTopic.TopicId, newTopic.ForumId, postIds });

                var oldTopicId = posts.First().TopicId;
                foreach (var post in posts)
                {
                    await _postService.CascadePostDelete(post, false);
                    post.TopicId = newTopic.TopicId;
                    post.ForumId = newTopic.ForumId;
                    await _postService.CascadePostAdd(post, false);
                    await _operationLogService.LogModeratorPostAction(ModeratorPostActions.MoveSelectedPosts, logDto.UserId, post.PostId, $"Moved from {oldTopicId} to {destinationTopicId}");
                }

                return (LanguageProvider.Moderator[await GetLanguage(), "POSTS_MOVED_SUCCESSFULLY"], true);
            }
            catch (Exception ex)
            {
                var id = Utils.HandleError(ex);
                return (string.Format(LanguageProvider.Errors[await GetLanguage(), "AN_ERROR_OCCURRED_TRY_AGAIN_ID_FORMAT"], id), false);
            }
        }

        public async Task<(string Message, bool? IsSuccess)> DeletePosts(int[] postIds, OperationLogDto logDto)
        {
            try
            {
                if (!(postIds?.Any() ?? false))
                {
                    return (LanguageProvider.Moderator[await GetLanguage(), "ATLEAST_ONE_POST_REQUIRED"], false);
                }

                var conn = _context.Database.GetDbConnection();

                var posts = (await conn.QueryAsync<PhpbbPosts>("SELECT * FROM phpbb_posts WHERE post_id IN @postIds ORDER BY post_time", new { postIds })).AsList();
                if (posts.Count != postIds.Length || posts.Select(p => p.TopicId).Distinct().Count() != 1)
                {
                    return (LanguageProvider.Moderator[await GetLanguage(), "ATLEAST_ONE_POST_MOVED_OR_DELETED"], false);
                }

                await conn.ExecuteAsync("DELETE FROM phpbb_posts WHERE post_id IN @postIds", new { postIds });
                foreach (var post in posts)
                {
                    await _postService.CascadePostDelete(post, false);
                    await _operationLogService.LogModeratorPostAction(ModeratorPostActions.DeleteSelectedPosts, logDto.UserId, post);
                }

                return (LanguageProvider.Moderator[await GetLanguage(), "POSTS_DELETED_SUCCESSFULLY"], true);
            }
            catch (Exception ex)
            {
                var id = Utils.HandleError(ex);
                return (string.Format(LanguageProvider.Errors[await GetLanguage(), "AN_ERROR_OCCURRED_TRY_AGAIN_ID_FORMAT"], id), false);
            }
        }

        public async Task<(string Message, bool? IsSuccess)> DuplicatePost(int postId, OperationLogDto logDto)
        {
            try
            {
                var post = await _context.PhpbbPosts.AsNoTracking().FirstOrDefaultAsync(p => p.PostId == postId);
                if (post == null)
                {
                    return (LanguageProvider.Moderator[await GetLanguage(), "ATLEAST_ONE_POST_REQUIRED"], false);
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
                        }).Where(a => a != null));

                    await _context.SaveChangesAsync();
                }

                await _postService.CascadePostAdd(entity.Entity, false);

                await _operationLogService.LogModeratorPostAction(ModeratorPostActions.DuplicateSelectedPost, logDto.UserId, postId);

                return (string.Empty, true);
            }
            catch (Exception ex)
            {
                var id = Utils.HandleError(ex);
                return (string.Format(LanguageProvider.Errors[await GetLanguage(), "AN_ERROR_OCCURRED_TRY_AGAIN_ID_FORMAT"], id), false);
            }
        }

        #endregion Post

        public async Task<IEnumerable<Tuple<int, string>>> GetReportedMessages()
        {
            var connection = _context.Database.GetDbConnection();
            return (await connection.QueryAsync(
                @"SELECT r.post_id, jr.reason_title
                    FROM phpbb_reports r
                    JOIN phpbb_reports_reasons jr ON r.reason_id = jr.reason_id
                    WHERE r.report_closed = 0"
            )).Select(r => Tuple.Create((int)r.post_id, (string)r.reason_title));
        }
    }
}
