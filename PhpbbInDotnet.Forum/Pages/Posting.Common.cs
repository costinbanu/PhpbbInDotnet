using Dapper;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PhpbbInDotnet.Database.Entities;
using PhpbbInDotnet.Domain;
using PhpbbInDotnet.Domain.Extensions;
using PhpbbInDotnet.Domain.Utilities;
using PhpbbInDotnet.Forum.Models;
using PhpbbInDotnet.Objects;
using Polly;
using Polly.Retry;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;

namespace PhpbbInDotnet.Forum.Pages
{
    public partial class PostingModel : BasePostingModel
    {
        public async Task<PostListDto?> GetPreviousPosts()
        {
            if (TopicId > 0 && (Action == PostingActions.EditForumPost || Action == PostingActions.NewForumPost))
            {
                return await _postService.GetPosts(TopicId ?? 0, pageNum: 1, Constants.DEFAULT_PAGE_SIZE, isPostingView: true, Language);
            }
            return null;
        }

        private async Task<PhpbbAttachments?> DeleteAttachment(int attachId, bool removeFromList)
        {
            var search = Attachments?.Indexed().FirstOrDefault(a => a.Item.AttachId == attachId);
            
            if (search?.Item is null)
            {
                return null;
            }

            var attachment = search.Value.Item;
            var index = search.Value.Index;
            if (!await _storageService.DeleteAttachment(attachment.PhysicalFilename))
            {
                return null;
            }

            if (!string.IsNullOrWhiteSpace(PostText))
            {
                PostText = Regex.Replace(
					input: PostText,
					pattern: $@"\[attachment={index}\]{Regex.Escape(attachment.RealFilename)}\[\/attachment\]",
					replacement: string.Empty,
					options: RegexOptions.IgnoreCase);
				for (int i = index + 1; i < Attachments!.Count; i++)
                {
                    PostText = Regex.Replace(
                        input: PostText,
                        pattern: $@"\[attachment={i}\]{Regex.Escape(Attachments[i].RealFilename)}\[\/attachment\]",
                        replacement: $"[attachment={i - 1}]{Attachments[i].RealFilename}[/attachment]",
                        options: RegexOptions.IgnoreCase);
                }
            }

            await SqlExecuter.ExecuteAsync(
                "DELETE FROM phpbb_attachments WHERE attach_id = @attachId", 
                new { attachment.AttachId });

            if (removeFromList)
            {
                Attachments!.Remove(attachment);
            }

            return attachment;
        }

        private async Task<int?> UpsertPost(PhpbbPosts? post, ForumUserExpanded usr)
        {
            var lang = Language;
            var curTopic = Action != PostingActions.NewTopic ? await SqlExecuter.QuerySingleOrDefaultAsync<PhpbbTopics>("SELECT * FROM phpbb_topics WHERE topic_id = @topicId", new { TopicId }) : null;
            var canCreatePoll = Action == PostingActions.NewTopic || (Action == PostingActions.EditForumPost && curTopic?.TopicFirstPostId == PostId);

            if (curTopic?.TopicStatus == 1 && !await UserService.IsUserModeratorInForum(ForumUser, ForumId))
            {
                var key = Action == PostingActions.EditForumPost ? "CANT_EDIT_POST_TOPIC_CLOSED" : "CANT_SUBMIT_POST_TOPIC_CLOSED";
                ModelState.AddModelError(nameof(PostText), TranslationProvider.Errors[lang, key, Casing.FirstUpper]);
                ShowPoll = canCreatePoll;
                return null;
            }
            
            if (canCreatePoll && (string.IsNullOrWhiteSpace(PollExpirationDaysString) || !double.TryParse(PollExpirationDaysString, out var val) || val < 0 || val > 365))
            {
                ModelState.AddModelError(nameof(PollExpirationDaysString), TranslationProvider.Errors[lang, "INVALID_POLL_EXPIRATION"]);
                ShowPoll = true;
                return null;
            }

            if (canCreatePoll && (PollMaxOptions == null || (PollOptionsEnumerable.Any() && (PollMaxOptions < 1 || PollMaxOptions > PollOptionsEnumerable.Count()))))
            {
                ModelState.AddModelError(nameof(PollMaxOptions), TranslationProvider.Errors[lang, "INVALID_POLL_OPTION_COUNT"]);
                ShowPoll = true;
                return null;
            }

            var isNewPost = post is null;

            await Policy.Handle<Exception>()
                .RetryAsync((ex, _) => _logger.Warning(ex, "Error while posting, will retry once."))
                .ExecuteAsync(async () =>
                {
                    using var transaction = SqlExecuter.BeginTransaction(IsolationLevel.Serializable);
                    if (Action == PostingActions.NewTopic)
                    {
                        curTopic = await transaction.QuerySingleAsync<PhpbbTopics>(
                            @$"INSERT INTO phpbb_topics (forum_id, topic_title, topic_time) 
                               VALUES (@forumId, @postTitle, @now); 
                               SELECT * 
                                 FROM phpbb_topics 
                                WHERE topic_id = {SqlExecuter.LastInsertedItemId};",
                            new { ForumId, PostTitle, now = DateTime.UtcNow.ToUnixTimestamp() });
                        TopicId = curTopic.TopicId;
                    }

                    var hasAttachments = Attachments?.Any() == true;
                    var textForSaving = await _writingService.PrepareTextForSaving(HttpUtility.HtmlEncode(PostText?.Trim()), transaction);
                    if (isNewPost)
                    {
                        post = await transaction.QuerySingleAsync<PhpbbPosts>(
                            @$"INSERT INTO phpbb_posts (forum_id, topic_id, poster_id, post_subject, post_text, post_time, post_attachment, post_checksum, poster_ip, post_username) 
                               VALUES (@forumId, @topicId, @userId, @subject, @textForSaving, @now, @attachment, @checksum, @ip, @username); 
                               SELECT * 
                                 FROM phpbb_posts 
                                WHERE post_id = {SqlExecuter.LastInsertedItemId};",
                            new
                            {
                                ForumId,
                                topicId = TopicId!.Value,
                                usr.UserId,
                                subject = HttpUtility.HtmlEncode(PostTitle),
                                textForSaving,
                                now = DateTime.UtcNow.ToUnixTimestamp(),
                                attachment = hasAttachments.ToByte(),
                                checksum = HashUtility.ComputeMD5Hash(textForSaving),
                                ip = HttpContext.GetIpAddress() ?? string.Empty,
                                username = HttpUtility.HtmlEncode(usr.Username)
                            });

                        await _postService.CascadePostAdd(transaction, ignoreUser: false, ignoreForums: false, post);
                    }
                    else
                    {
                        post = await transaction.QuerySingleAsync<PhpbbPosts>(
                            @"UPDATE phpbb_posts 
                                 SET post_subject = @subject, post_text = @textForSaving, post_attachment = @attachment, post_checksum = @checksum, post_edit_time = @now, post_edit_reason = @reason, post_edit_user = @userId, post_edit_count = post_edit_count + 1 
                               WHERE post_id = @postId; 
                              SELECT * 
                                FROM phpbb_posts 
                               WHERE post_id = @postId;",
                            new
                            {
                                subject = HttpUtility.HtmlEncode(PostTitle),
                                textForSaving,
                                checksum = HashUtility.ComputeMD5Hash(textForSaving),
                                attachment = hasAttachments.ToByte(),
                                post!.PostId,
                                now = DateTime.UtcNow.ToUnixTimestamp(),
                                reason = HttpUtility.HtmlEncode(EditReason ?? string.Empty),
                                usr.UserId
                            });

                        if (curTopic?.TopicFirstPostId == post.PostId)
                        {
                            await _postService.CascadePostEdit(post, transaction);
                        }
                    }

                    foreach (var attach in Attachments!)
                    {
                        await transaction.ExecuteAsync(
                            @"UPDATE phpbb_attachments 
                                 SET post_msg_id = @postId, topic_id = @topicId, attach_comment = @comment, is_orphan = 0, order_in_post = @orderInPost
                               WHERE attach_id = @attachId",
                            new
                            {
                                post.PostId,
                                post.TopicId,
                                comment = await _writingService.PrepareTextForSaving(attach.AttachComment, transaction),
                                attach.AttachId,
                                attach.OrderInPost
                            });
                    }

                    if (canCreatePoll && !string.IsNullOrWhiteSpace(PollOptions))
                    {
                        var existing = await transaction.QueryAsync<string>(
                            "SELECT LTRIM(RTRIM(poll_option_text)) FROM phpbb_poll_options WHERE topic_id = @topicId",
                            new { TopicId });
                        if (!existing.SequenceEqual(PollOptionsEnumerable, StringComparer.InvariantCultureIgnoreCase))
                        {
                            await transaction.ExecuteAsync(
                                @"DELETE FROM phpbb_poll_options WHERE topic_id = @topicId;
                                  DELETE FROM phpbb_poll_votes WHERE topic_id = @topicId",
                                new { TopicId });

                            foreach (var (option, id) in PollOptionsEnumerable.Indexed(startIndex: 1))
                            {
                                await transaction.ExecuteAsync(
                                    @"INSERT INTO phpbb_poll_options (poll_option_id, topic_id, poll_option_text, poll_option_total) 
                                      VALUES (@id, @topicId, @text, 0)",
                                    new { id, TopicId, text = HttpUtility.HtmlEncode(option) });
                            }
                        }
                        await transaction.ExecuteAsync(
                            @"UPDATE phpbb_topics 
                                 SET poll_start = @start, poll_length = @length, poll_max_options = @maxOptions, poll_title = @title, poll_vote_change = @change 
                               WHERE topic_id = @topicId",
                            new
                            {
                                start = curTopic!.PollStart == 0 ? DateTime.UtcNow.ToUnixTimestamp() : curTopic.PollStart,
                                length = (int)TimeSpan.FromDays(double.TryParse(PollExpirationDaysString, out var exp) ? exp : 1d).TotalSeconds,
                                maxOptions = (byte)(PollMaxOptions ?? 1),
                                title = HttpUtility.HtmlEncode(PollQuestion),
                                change = PollCanChangeVote.ToByte(),
                                TopicId
                            });
                    }

                    if (Action == PostingActions.NewTopic || Action == PostingActions.NewForumPost)
                    {
                        await transaction.ExecuteAsync(
                            "DELETE FROM phpbb_drafts WHERE user_id = @userId AND forum_id = @forumId AND topic_id = @topicId",
                            new { usr.UserId, forumId = ForumId, topicId = Action == PostingActions.NewTopic ? 0 : TopicId });
                    }

                    transaction.CommitTransaction();

                    await _cachedDbInfoService.ForumTopicCount.InvalidateAsync();
                    await _cachedDbInfoService.ForumTree.InvalidateAsync();
                });

            Response.Cookies.DeleteObject(CookieBackupKey);

            if (Action == PostingActions.NewTopic || Action == PostingActions.NewForumPost)
            {
                try
                {
                    var tree = await ForumService.GetForumTree(ForumUser, forceRefresh: false, fetchUnreadData: false);
                    var path = ForumService.GetPathText(tree, post!.ForumId);
                    await _notificationService.SendNewPostNotification(post.PosterId, post.ForumId, post.TopicId, post.PostId, path, curTopic!.TopicTitle);
                }
                catch (Exception ex)
                {
                    _logger.Warning(ex, "Failed to notify subscribers");
                }
            }

            return post!.PostId;
        }

        private async Task<IActionResult> WithNewestPostSincePageLoad(PhpbbForums curForum, Func<Task<IActionResult>> toDo)
        {
            var lang = Language;

            if (TopicId > 0 && LastPostTime > 0 && Action != PostingActions.EditForumPost)
            {
                (long? postTime, long? postEditTime) = await SqlExecuter.QueryFirstOrDefaultAsync<(long? postTime, long? postEditTime)>(
                    @"SELECT p.post_time, p.post_edit_time
                        FROM phpbb_posts p
                        JOIN phpbb_topics t ON p.post_id = t.topic_last_post_id
                       WHERE t.topic_id = @topicId",
                    new { TopicId });
                if (postTime > LastPostTime)
                {
                    return PageWithError(curForum, nameof(LastPostTime), TranslationProvider.Errors[lang, "NEW_MESSAGES_SINCE_LOAD"]);
                }
                else if (postEditTime > LastPostTime)
                {
                    LastPostTime = postEditTime;
                    return PageWithError(curForum, nameof(LastPostTime), TranslationProvider.Errors[lang, "LAST_MESSAGE_WAS_EDITED_SINCE_LOAD"]);
                }
                else
                {
                    ModelState[nameof(LastPostTime)]?.Errors.Clear();
                }
            }
            return await toDo();
        }

        private Task<IActionResult> WithRegisteredUserAndCorrectPermissions(Func<ForumUserExpanded, Task<IActionResult>> toDo)
            => WithRegisteredUser(async user =>
            {
                ThrowIfEntireForumIsReadOnly();
                if (await ForumService.IsForumReadOnlyForUser(user, ForumId))
                {
                    return Unauthorized();
                }
                return await toDo(user);
            });

        protected override IActionResult PageWithError(PhpbbForums curForum, string errorKey, string errorMessage)
            => PageWithError(errorKey, errorMessage, () => CurrentForum = curForum);

        private PageResult BackedUpPage()
        {
            SaveBackup();
            return Page();
        }

        private Task<IActionResult> WithInitialBackup(Func<Task<IActionResult>> toDo)
        {
            SaveBackup();
            return toDo();
        }

        private void SaveBackup()
            => Response.Cookies.AddObject(
                key: CookieBackupKey,
                value: new PostingBackup(Action!.Value, PostTitle, PostText, DateTime.UtcNow, ForumId, TopicId, PostId, Attachments?.Select(a => a.AttachId).ToList(), QuotePostInDifferentTopic, AttachmentOrder),
                maxAge: _cookieBackupExpiration);

        private async Task RestoreBackupIfAny(DateTime? minCacheAge = null)
        {
            if (Request.Cookies.TryGetObject<PostingBackup>(CookieBackupKey, out var cookie))
            {
                Action = cookie.PostingActions;
                ForumId = cookie.ForumId != 0 ? cookie.ForumId : ForumId;
                TopicId ??= cookie.TopicId;
                PostId ??= cookie.PostId;
                QuotePostInDifferentTopic = cookie.QuotePostInDifferentTopic;

                if ((!string.IsNullOrWhiteSpace(cookie.Text) && string.IsNullOrWhiteSpace(PostText)) ||
                    (!string.IsNullOrWhiteSpace(cookie.Text) && cookie.TextTime > minCacheAge))
                {
                    PostText = cookie.Text;
                }

                if ((!string.IsNullOrWhiteSpace(cookie.Title) && string.IsNullOrWhiteSpace(PostTitle)) ||
                    (!string.IsNullOrWhiteSpace(cookie.Title) && cookie.TextTime > minCacheAge))
                {
                    PostTitle = cookie.Title;
                }

                if ((Attachments?.Any() != true && cookie.AttachmentIds?.Any() == true) ||
                    (cookie.AttachmentIds?.Any() == true && cookie.TextTime > minCacheAge))
                {
                    Attachments = (await SqlExecuter.QueryAsync<PhpbbAttachments>(
                        "SELECT * FROM phpbb_attachments WHERE attach_id IN @attachmentIds ORDER BY order_in_post",
                        new { cookie.AttachmentIds })).AsList();
                }

                if ((AttachmentOrder?.Any() != true && cookie.AttachmentOrder?.Any() == true) ||
                    (cookie.AttachmentOrder?.Any() == true && cookie.TextTime > minCacheAge))
                {
                    AttachmentOrder = cookie.AttachmentOrder;
                    ReorderModelAttachments();
                }
            }
        }

        private void RemoveDraftFromModelState()
        {
			var keysToRemove = ModelState.Keys.Where(k => k.StartsWith(nameof(ExistingPostDraft)));
			foreach (var keyToRemove in keysToRemove)
			{
				ModelState.Remove(keyToRemove);
			}
		}

        private void ReorderModelAttachmentsIfNeeded()
        {
            if (!AttachmentOrderHasChanged)
            {
                return;
            }

            ReorderModelAttachments();
		}

        private void ReorderModelAttachments()
        {
            var orderDict = AttachmentOrder?.Indexed().ToDictionary(k => k.Item, v => v.Index) ?? [];
            foreach (var attach in Attachments ?? [])
            {
                if (orderDict.TryGetValue(attach.AttachId, out var order))
                {
                    attach.OrderInPost = order;
                }
            }

            Attachments = Attachments?.OrderBy(attach => attach.OrderInPost).ToList();

            var keysToRemove = ModelState.Keys.Where(k => k.StartsWith(nameof(Attachments)));
            foreach (var keyToRemove in keysToRemove)
            {
                ModelState.Remove(keyToRemove);
            }
        }

        private async Task ReorderModelAndDatabaseAttachmentsIfNeeded()
        {
			if (!AttachmentOrderHasChanged)
			{
				return;
			}

            ReorderModelAttachments();

			foreach (var attach in Attachments!)
			{
				await SqlExecuter.ExecuteAsync(
					@"UPDATE phpbb_attachments 
                         SET order_in_post = @orderInPost
                       WHERE attach_id = @attachId",
				new
				{
					attach.AttachId,
					attach.OrderInPost
				});
			}
        }
    }
}