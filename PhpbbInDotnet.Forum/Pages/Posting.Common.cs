using Dapper;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using PhpbbInDotnet.Database.Entities;
using PhpbbInDotnet.Domain;
using PhpbbInDotnet.Domain.Extensions;
using PhpbbInDotnet.Domain.Utilities;
using PhpbbInDotnet.Forum.Models;
using PhpbbInDotnet.Objects;
using System;
using System.Data;
using System.Linq;
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

        private async Task<PhpbbAttachments?> DeleteAttachment(int index, bool removeFromList)
        {
            var attachment = Attachments?.ElementAtOrDefault(index);

            if (attachment is null)
            {
                return null;
            }

            if (!await _storageService.DeleteAttachment(attachment.PhysicalFilename))
            {
                return null;
            }

            if (!string.IsNullOrWhiteSpace(PostText))
            {
                PostText = PostText.Replace($"[attachment={index}]{attachment.RealFilename}[/attachment]", string.Empty, StringComparison.InvariantCultureIgnoreCase);
                for (int i = index + 1; i < Attachments!.Count; i++)
                {
                    PostText = PostText.Replace($"[attachment={i}]{Attachments[i].RealFilename}[/attachment]", $"[attachment={i - 1}]{Attachments[i].RealFilename}[/attachment]", StringComparison.InvariantCultureIgnoreCase);
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

            if (Action == PostingActions.NewTopic)
            {
                curTopic = await SqlExecuter.QueryFirstOrDefaultAsync<PhpbbTopics>(
                    @"INSERT INTO phpbb_topics (forum_id, topic_title, topic_time) 
                      VALUES (@forumId, @postTitle, @now); 
                      SELECT * 
                        FROM phpbb_topics 
                       WHERE topic_id = LAST_INSERT_ID();", 
                    new { ForumId, PostTitle, now = DateTime.UtcNow.ToUnixTimestamp() });
                TopicId = curTopic.TopicId;
            }

            var hasAttachments = Attachments?.Any() == true;
            var textForSaving = await _writingService.PrepareTextForSaving(HttpUtility.HtmlEncode(PostText));
            if (post == null)
            {
                post = await SqlExecuter.QueryFirstOrDefaultAsync<PhpbbPosts>(
                    @"INSERT INTO phpbb_posts (forum_id, topic_id, poster_id, post_subject, post_text, post_time, post_attachment, post_checksum, poster_ip, post_username) 
                      VALUES (@forumId, @topicId, @userId, @subject, @textForSaving, @now, @attachment, @checksum, @ip, @username); 
                      SELECT * 
                        FROM phpbb_posts 
                       WHERE post_id = LAST_INSERT_ID();",
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
                        ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? string.Empty,
                        username = HttpUtility.HtmlEncode(usr.Username)
                    });

                await _postService.CascadePostAdd(post, false);
            }
            else
            {
                post = await SqlExecuter.QueryFirstOrDefaultAsync<PhpbbPosts>(
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
                        post.PostId,
                        now = DateTime.UtcNow.ToUnixTimestamp(),
                        reason = HttpUtility.HtmlEncode(EditReason ?? string.Empty),
                        usr.UserId
                    });

                await _postService.CascadePostEdit(post);
            }

            foreach (var attach in Attachments!)
            {
                await SqlExecuter.ExecuteAsync(
                    @"UPDATE phpbb_attachments 
                         SET post_msg_id = @postId, topic_id = @topicId, attach_comment = @comment, is_orphan = 0 
                       WHERE attach_id = @attachId",
                    new
                    {
                        post.PostId,
                        post.TopicId,
                        comment = await _writingService.PrepareTextForSaving(attach.AttachComment),
                        attach.AttachId
                    });
            }

            if (canCreatePoll && !string.IsNullOrWhiteSpace(PollOptions))
            {
                var existing = await SqlExecuter.QueryAsync<string>("SELECT LTRIM(RTRIM(poll_option_text)) FROM phpbb_poll_options WHERE topic_id = @topicId", new { TopicId });
                if (!existing.SequenceEqual(PollOptionsEnumerable, StringComparer.InvariantCultureIgnoreCase))
                {
                    await SqlExecuter.ExecuteAsync(
                        @"DELETE FROM phpbb_poll_options WHERE topic_id = @topicId;
                          DELETE FROM phpbb_poll_votes WHERE topic_id = @topicId",
                        new { TopicId });

                    foreach (var (option, id) in PollOptionsEnumerable.Indexed(startIndex: 1))
                    {
                        await SqlExecuter.ExecuteAsync(
                            @"INSERT INTO phpbb_poll_options (poll_option_id, topic_id, poll_option_text, poll_option_total) 
                              VALUES (@id, @topicId, @text, 0)",
                            new { id, TopicId, text = HttpUtility.HtmlEncode(option) });
                    }
                }
                await SqlExecuter.ExecuteAsync(
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
                await SqlExecuter.ExecuteAsync(
                    "DELETE FROM phpbb_drafts WHERE user_id = @userId AND forum_id = @forumId AND topic_id = @topicId",
                    new { usr.UserId, forumId = ForumId, topicId = Action == PostingActions.NewTopic ? 0 : TopicId });
            }

            Response.Cookies.DeleteObject(CookieBackupKey);

            return post.PostId;
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
                if (await ForumService.IsForumReadOnlyForUser(user, ForumId))
                {
                    return Unauthorized();
                }
                return await toDo(user);
            });

        protected override IActionResult PageWithError(PhpbbForums curForum, string errorKey, string errorMessage)
            => PageWithError(errorKey, errorMessage, () => CurrentForum = curForum);

        private Task<IActionResult> WithBackup(Func<Task<IActionResult>> toDo)
        {
            Response.Cookies.AddObject(
                key: CookieBackupKey,
                value: new PostingBackup(PostText, DateTime.UtcNow, ForumId, TopicId ?? 0, PostId ?? 0, Attachments?.Select(a => a.AttachId).ToList()),
                maxAge: _cookieBackupExpiration);
            return toDo();
        }

        private async Task RestoreBackupIfAny(DateTime? minCacheAge = null)
        {
            if (Request.Cookies.TryGetObject<PostingBackup>(CookieBackupKey, out var cookie))
            {
                if ((!string.IsNullOrWhiteSpace(cookie.Text) && string.IsNullOrWhiteSpace(PostText)) ||
                    (!string.IsNullOrWhiteSpace(cookie.Text) && cookie.TextTime > minCacheAge))
                {
                    PostText = cookie.Text;
                }
                ForumId = cookie.ForumId != 0 ? cookie.ForumId : ForumId;
                TopicId ??= cookie.TopicId;
                PostId ??= cookie.PostId;
                if (Attachments?.Any() != true && cookie.AttachmentIds?.Any() == true)
                {
                    Attachments = (await SqlExecuter.QueryAsync<PhpbbAttachments>(
                        "SELECT * FROM phpbb_attachments WHERE attach_id IN @attachmentIds",
                        new { cookie.AttachmentIds })).AsList();
                }
            }
        }
    }
}