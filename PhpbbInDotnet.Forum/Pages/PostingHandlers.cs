using Dapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using PhpbbInDotnet.Objects;
using PhpbbInDotnet.Database.Entities;
using PhpbbInDotnet.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;

namespace PhpbbInDotnet.Forum.Pages
{
    public partial class PostingModel
    {
        #region GET

        public async Task<IActionResult> OnGetForumPost()
            => await WithRegisteredUser(async (user) => await WithValidTopic(TopicId ?? 0, async (curForum, curTopic) =>
            {
                CurrentForum = curForum;
                CurrentTopic = curTopic;
                ForumId = curForum.ForumId;
                Action = PostingActions.NewForumPost;
                await Init();
                var conn = _context.Database.GetDbConnection();
                await conn.OpenIfNeededAsync();
                var draft = conn.QueryFirstOrDefault<PhpbbDrafts>("SELECT * FROM phpbb_drafts WHERE user_id = @userId AND forum_id = @forumId AND topic_id = @topicId", new { user.UserId, ForumId, topicId = TopicId.Value });
                if (draft != null)
                {
                    PostTitle = HttpUtility.HtmlDecode(draft.DraftSubject);
                    PostText = HttpUtility.HtmlDecode(draft.DraftMessage);
                }
                else
                {
                    PostTitle = $"{Constants.REPLY}{HttpUtility.HtmlDecode(curTopic.TopicTitle)}";
                }
                await RestoreBackupIfAny(draft?.SaveTime.ToUtcTime());
                return Page();
            }));

        public async Task<IActionResult> OnGetQuoteForumPost()
            => await WithRegisteredUser(async (user) => await WithValidPost(PostId ?? 0, async (curForum, curTopic, curPost) =>
            {
                var curAuthor = curPost.PostUsername;
                if (string.IsNullOrWhiteSpace(curAuthor))
                {
                    var conn = _context.Database.GetDbConnection();
                    await conn.OpenIfNeededAsync();
                    curAuthor = (await conn.QueryFirstOrDefaultAsync<PhpbbUsers>("SELECT * FROM phpbb_users WHERE user_id = @posterId", new { curPost.PosterId }))?.Username ?? "Anonymous";
                }

                CurrentForum = curForum;
                CurrentTopic = curTopic;
                ForumId = curForum.ForumId;
                Action = PostingActions.NewForumPost;
                await Init();

                var title = HttpUtility.HtmlDecode(curPost.PostSubject);
                PostText = $"[quote=\"{curAuthor}\"]\n{_writingService.CleanBbTextForDisplay(curPost.PostText, curPost.BbcodeUid)}\n[/quote]";
                PostTitle = title.StartsWith(Constants.REPLY) ? title : $"{Constants.REPLY}{title}";
                await RestoreBackupIfAny();
                return Page();
            }));

        public async Task<IActionResult> OnGetNewTopic()
            => await WithRegisteredUser(async (user) => await WithValidForum(ForumId, async (curForum) =>
            {
                CurrentForum = curForum;
                CurrentTopic = null;
                Action = PostingActions.NewTopic;
                await Init();
                var conn = _context.Database.GetDbConnection();
                await conn.OpenIfNeededAsync();
                var draft = conn.QueryFirstOrDefault<PhpbbDrafts>("SELECT * FROM phpbb_drafts WHERE user_id = @userId AND forum_id = @forumId AND topic_id = @topicId", new { user.UserId, ForumId, topicId = 0 }); 
                if (draft != null)
                {
                    PostTitle = HttpUtility.HtmlDecode(draft.DraftSubject);
                    PostText = HttpUtility.HtmlDecode(draft.DraftMessage);
                }
                await RestoreBackupIfAny(draft?.SaveTime.ToUtcTime());
                return Page();
            }));

        public async Task<IActionResult> OnGetEditPost()
            => await WithRegisteredUser(async (user) => await WithValidPost(PostId ?? 0, async (curForum, curTopic, curPost) =>
            {
                if (!(await IsCurrentUserModeratorHere() || (curPost.PosterId == user.UserId && (user.PostEditTime == 0 || DateTime.UtcNow.Subtract(curPost.PostTime.ToUtcTime()).TotalMinutes <= user.PostEditTime))))
                {
                    return RedirectToPage("ViewTopic", "byPostId", new { PostId });
                }

                var canCreatePoll = curTopic.TopicFirstPostId == PostId;

                CurrentForum = curForum;
                CurrentTopic = curTopic;
                ForumId = curForum.ForumId;
                Action = PostingActions.EditForumPost;
                await Init();

                var conn = _context.Database.GetDbConnection();
                await conn.OpenIfNeededAsync();

                Attachments = (await conn.QueryAsync<PhpbbAttachments>("SELECT * FROM phpbb_attachments WHERE post_msg_id = @postId", new { PostId })).AsList();
                ShowAttach = Attachments.Any();

                await _cacheService.SetInCache(await GetActualCacheKey("PostTime", true), curPost.PostTime);

                if (canCreatePoll && curTopic.PollStart > 0)
                {
                    var pollOptionsText = (await conn.QueryAsync<string>("SELECT poll_option_text FROM phpbb_poll_options WHERE topic_id = @topicId", new { curTopic.TopicId })).AsList();
                    PollQuestion = curTopic.PollTitle;
                    PollOptions = string.Join(Environment.NewLine, pollOptionsText);
                    PollCanChangeVote = curTopic.PollVoteChange.ToBool();
                    PollExpirationDaysString = TimeSpan.FromSeconds(curTopic.PollLength).TotalDays.ToString();
                    PollMaxOptions = curTopic.PollMaxOptions;
                    ShowPoll = true;
                }

                var subject = curPost.PostSubject.StartsWith(Constants.REPLY) ? curPost.PostSubject.Substring(Constants.REPLY.Length) : curPost.PostSubject;
                PostText = _writingService.CleanBbTextForDisplay(curPost.PostText, curPost.BbcodeUid);
                PostTitle = HttpUtility.HtmlDecode(curPost.PostSubject);
                PostTime = curPost.PostTime;
                await RestoreBackupIfAny();
                return Page();
            }));

        public async Task<IActionResult> OnGetPrivateMessage()
            => await WithRegisteredUser(async (usr) =>
            {
                var conn = _context.Database.GetDbConnection();
                await conn.OpenIfNeededAsync();

                if ((PostId ?? 0) > 0)
                {
                    var post = await conn.QueryFirstOrDefaultAsync<PhpbbPosts>("SELECT * FROM phpbb_posts WHERE post_id = @postId", new { PostId });
                    if (post != null)
                    {
                        var author = await conn.QueryFirstOrDefaultAsync<PhpbbUsers>("SELECT * FROM phpbb_users WHERE user_id = @posterId", new { post.PosterId });
                        if ((author?.UserId ?? Constants.ANONYMOUS_USER_ID) != Constants.ANONYMOUS_USER_ID)
                        {
                            PostTitle = HttpUtility.HtmlDecode(post.PostSubject);
                            PostText = $"[quote]{_writingService.CleanBbTextForDisplay(post.PostText, post.BbcodeUid)}[/quote]\r\n[url=./ViewTopic?postId={PostId}&handler=byPostId]{PostTitle}[/url]";
                            ReceiverId = author.UserId;
                            ReceiverName = author.Username;
                        }
                        else
                        {
                            return BadRequest("Destinatarul nu există");
                        }
                    }
                    else
                    {
                        return BadRequest("Mesajul nu există");
                    }
                }
                else if ((PrivateMessageId ?? 0) > 0 && (ReceiverId ?? Constants.ANONYMOUS_USER_ID) != Constants.ANONYMOUS_USER_ID)
                {
                    var msg = await conn.QueryFirstOrDefaultAsync<PhpbbPrivmsgs>("SELECT * FROM phpbb_privmsgs WHERE msg_id = @privateMessageId", new { PrivateMessageId });
                    if (msg != null && ReceiverId == msg.AuthorId)
                    {
                        var author = await conn.QueryFirstOrDefaultAsync<PhpbbUsers>("SELECT * FROM phpbb_users WHERE user_id = @receiverId", new { ReceiverId});
                        if ((author?.UserId ?? Constants.ANONYMOUS_USER_ID) != Constants.ANONYMOUS_USER_ID)
                        {
                            var title = HttpUtility.HtmlDecode(msg.MessageSubject);
                            PostTitle = title.StartsWith(Constants.REPLY) ? title : $"{Constants.REPLY}{title}";
                            PostText = $"[quote]{_writingService.CleanBbTextForDisplay(msg.MessageText, msg.BbcodeUid)}[/quote]";
                            ReceiverName = author.Username;
                        }
                        else
                        {
                            return BadRequest("Destinatarul nu există");
                        }
                    }
                    else
                    {
                        return BadRequest("Mesajul nu există");
                    }
                }
                else if ((ReceiverId ?? Constants.ANONYMOUS_USER_ID) != Constants.ANONYMOUS_USER_ID)
                {
                    ReceiverName = (await conn.QueryFirstOrDefaultAsync<PhpbbUsers>("SELECT * FROM phpbb_users WHERE user_id = @receiverId", new { ReceiverId }))?.Username;
                }

                CurrentForum = null;
                CurrentTopic = null;
                Action = PostingActions.NewPrivateMessage;
                await Init();
                return Page();
            });

        public async Task<IActionResult> OnGetEditPrivateMessage()
            => await WithRegisteredUser(async (user) =>
            {
                var conn = _context.Database.GetDbConnection();
                await conn.OpenIfNeededAsync();

                var pm = await conn.QueryFirstOrDefaultAsync<PhpbbPrivmsgs>("SELECT * FROM phpbb_privmsgs WHERE msg_id = @privateMessageId", new { PrivateMessageId });
                PostText = _writingService.CleanBbTextForDisplay(pm.MessageText, pm.BbcodeUid);
                PostTitle = HttpUtility.HtmlDecode(pm.MessageSubject);
                if ((ReceiverId ?? Constants.ANONYMOUS_USER_ID) != Constants.ANONYMOUS_USER_ID)
                {
                    ReceiverName = (await conn.QueryFirstOrDefaultAsync<PhpbbUsers>("SELECT * FROM phpbb_users WHERE user_id = @receiverId", new { ReceiverId }))?.Username;
                }
                Action = PostingActions.EditPrivateMessage;
                await Init();
                return Page();
            });

        #endregion GET

        #region POST Attachment

        public async Task<IActionResult> OnPostAddAttachment()
            => await WithBackup(async () => await WithRegisteredUser(async (user) => await WithValidForum(ForumId, async (curForum) =>
            {
                CurrentForum = curForum;
                ShowAttach = true;

                if (!(Files?.Any() ?? false))
                {
                    return Page();
                }

                var tooLargeFiles = Files.Where(f => f.Length > 1024 * 1024 * (f.ContentType.StartsWith("image/", StringComparison.InvariantCultureIgnoreCase) ? _config.GetValue<int>("UploadLimitsMB:Images") : _config.GetValue<int>("UploadLimitsMB:OtherFiles")));
                if (tooLargeFiles.Any() && !await IsCurrentUserAdminHere())
                {
                    return PageWithError(curForum, nameof(Files), $"Următoarele fișiere sunt prea mari: {string.Join(",", tooLargeFiles.Select(f => f.FileName))}");
                }

                if ((Attachments?.Count ?? 0) + Files.Count() > 10 && !await IsCurrentUserAdminHere())
                {
                    return PageWithError(curForum, nameof(Files), "Sunt permise maxim 10 fișiere per mesaj.");
                }

                var (succeeded, failed) = await _storageService.BulkAddAttachments(Files, user.UserId);

                if (failed.Any())
                {
                    return PageWithError(curForum, nameof(Files), $"Următoarele fișiere nu au putut fi adăugate, vă rugăm să încercați din nou: {string.Join(",", failed)}");
                }

                if (Attachments == null)
                {
                    Attachments = succeeded;
                }
                else
                {
                    Attachments.AddRange(succeeded);
                }

                return Page();
            })));

        public async Task<IActionResult> OnPostDeleteAttachment(int index)
            => await WithBackup(async () => await WithRegisteredUser(async (user) => await WithValidForum(ForumId, async (curForum) =>
            {
                var attachment = Attachments?.ElementAtOrDefault(index);
                CurrentForum = curForum;

                if (attachment == null)
                {
                    return PageWithError(curForum, $"{nameof(DeleteFileDummyForValidation)}[{index}]", "Fișierul nu a putut fi șters, vă rugăm încercați din nou.");
                }

                if (!_storageService.DeleteFile(attachment.PhysicalFilename, false))
                {
                    ModelState.AddModelError($"{nameof(DeleteFileDummyForValidation)}[{index}]", "Fișierul nu a putut fi șters, vă rugăm încercați din nou.");
                }

                if (!string.IsNullOrWhiteSpace(PostText))
                {
                    PostText = PostText.Replace($"[attachment={index}]{attachment.RealFilename}[/attachment]", string.Empty, StringComparison.InvariantCultureIgnoreCase);
                    for (int i = index + 1; i < Attachments.Count; i++)
                    {
                        PostText = PostText.Replace($"[attachment={i}]{Attachments[i].RealFilename}[/attachment]", $"[attachment={i - 1}]{Attachments[i].RealFilename}[/attachment]", StringComparison.InvariantCultureIgnoreCase);
                    }
                }

                var connection = _context.Database.GetDbConnection();
                await connection.OpenIfNeededAsync();
                await connection.ExecuteAsync("DELETE FROM phpbb_attachments WHERE attach_id = @attachId", new { attachment.AttachId });
                var dummy = Attachments.Remove(attachment);
                ShowAttach = Attachments.Any();
                ModelState.Clear();
                return Page();
            })));

        #endregion POST Attachment

        #region POST Message

        public async Task<IActionResult> OnPostPreview()
            => await WithBackup(async () => await WithRegisteredUser(async (user) => await WithValidForum(ForumId, Action == PostingActions.NewPrivateMessage, async (curForum) => await WithNewestPostSincePageLoad(curForum, async () =>
            {
                if ((PostTitle?.Trim()?.Length ?? 0) < 3)
                {
                    return PageWithError(curForum, nameof(PostTitle), "Titlul este prea scurt (minim 3 caractere, exclusiv spații).");
                }

                if ((PostText?.Trim()?.Length ?? 0) < 3)
                {
                    return PageWithError(curForum, nameof(PostText), "Mesajul este prea scurt (minim 3 caractere, exclusiv spații).");
                }

                var conn = _context.Database.GetDbConnection();
                await conn.OpenIfNeededAsync();

                var currentPost = Action == PostingActions.EditForumPost ? await InitEditedPost() : null;
                var userId = Action == PostingActions.EditForumPost ? currentPost.PosterId : user.UserId;
                var postAuthor = await conn.QueryFirstOrDefaultAsync<PhpbbUsers>("SELECT * FROM phpbb_users WHERE user_id = @userId", new { userId });
                var rankId = postAuthor?.UserRank ?? 0;
                var newPostText = PostText;
                var uid = string.Empty;

                if (_config.GetValue<bool>("CompatibilityMode"))
                {
                    (newPostText, uid, _) = _renderingService.TransformForBackwardsCompatibility(newPostText);
                }
                else
                {
                    newPostText = HttpUtility.HtmlEncode(newPostText);
                }

                PreviewablePost = new PostDto
                {
                    Attachments = Attachments?.Select(a => new AttachmentDto(a.RealFilename, a.AttachComment, 0, a.Mimetype, 0, a.Filesize, a.PhysicalFilename, true))?.ToList() ?? new List<AttachmentDto>(),
                    AuthorColor = postAuthor.UserColour,
                    AuthorHasAvatar = !string.IsNullOrWhiteSpace(postAuthor?.UserAvatar),
                    AuthorId = postAuthor.UserId,
                    AuthorName = postAuthor.Username,
                    AuthorRank = (await conn.QueryFirstOrDefaultAsync("SELECT * FROM phpbb_ranks WHERE rank_id = @rankId", new { rankId }))?.RankTitle,
                    BbcodeUid = uid,
                    PostCreationTime = Action == PostingActions.EditForumPost ? PostTime?.ToUtcTime() : DateTime.UtcNow,
                    EditCount = (short)(Action == PostingActions.EditForumPost ? (currentPost?.PostEditCount ?? 0) + 1 : 0),
                    LastEditReason = Action == PostingActions.EditForumPost ? currentPost?.PostEditReason : string.Empty,
                    LastEditTime = Action == PostingActions.EditForumPost ? DateTime.UtcNow.ToUnixTimestamp() : 0,
                    LastEditUser = Action == PostingActions.EditForumPost ? user.Username : string.Empty,
                    PostId = currentPost?.PostId ?? 0,
                    PostSubject = HttpUtility.HtmlEncode(PostTitle),
                    PostText = await _writingService.PrepareTextForSaving(newPostText)
                };

                if (!string.IsNullOrWhiteSpace(PollOptions))
                {
                    PreviewablePoll = new PollDto
                    {
                        PollTitle = HttpUtility.HtmlEncode(PollQuestion),
                        PollOptions = new List<PollOption>(PollOptions.Split(Environment.NewLine).Select(x => new PollOption { PollOptionText = HttpUtility.HtmlEncode(x) })),
                        VoteCanBeChanged = PollCanChangeVote,
                        PollDurationSecons = (int)TimeSpan.FromDays(double.Parse(PollExpirationDaysString)).TotalSeconds,
                        PollMaxOptions = PollMaxOptions ?? 1,
                        PollStart = PreviewablePost.PostCreationTime ?? DateTime.UtcNow
                    };
                }
                await _renderingService.ProcessPost(PreviewablePost, PageContext, HttpContext, true);
                ShowAttach = Attachments?.Any() ?? false;
                CurrentForum = curForum;
                return Page();
            }))));

        public async Task<IActionResult> OnPostNewForumPost()
            => await WithRegisteredUser(async (user) => await WithValidForum(ForumId, async (curForum) => await WithNewestPostSincePageLoad(curForum, async () =>
            {
                var addedPostId = await UpsertPost(null, user);
                if (addedPostId == null)
                {
                    return PageWithError(curForum, nameof(PostText), "A intervenit o eroare iar mesajul nu a fost publicat. Te rugăm să încerci din nou.");
                }
                return RedirectToPage("ViewTopic", "byPostId", new { postId = addedPostId });
            })));

        public async Task<IActionResult> OnPostEditForumPost()
            => await WithRegisteredUser(async (user) => await WithValidPost(PostId ?? 0, async (curForum, curTopic, curPost) =>
            {
                if (!(await IsCurrentUserModeratorHere() || (curPost.PosterId == user.UserId && (user.PostEditTime == 0 || DateTime.UtcNow.Subtract(curPost.PostTime.ToUtcTime()).TotalMinutes <= user.PostEditTime))))
                {
                    return RedirectToPage("ViewTopic", "byPostId", new { PostId });
                }

                var addedPostId = await UpsertPost(await InitEditedPost(), user);

                if (addedPostId == null)
                {
                    CurrentForum = curForum;
                    CurrentTopic = curTopic;
                    ForumId = curForum.ForumId;
                    Action = PostingActions.EditForumPost;
                    await Init();
                    return Page();
                }
                return RedirectToPage("ViewTopic", "byPostId", new { postId = addedPostId });
            }));

        public async Task<IActionResult> OnPostPrivateMessage()
            => await WithRegisteredUser(async (user) =>
            {
                if ((ReceiverId ?? 1) == 1)
                {
                    return PageWithError(null, nameof(ReceiverName), "Introduceți un destinatar valid.");
                }

                if ((PostTitle?.Trim()?.Length ?? 0) < 3)
                {
                    return PageWithError(null, nameof(PostTitle), "Subiectul este prea scurt (minim 3 caractere, exclusiv spații).");
                }

                if ((PostText?.Trim()?.Length ?? 0) < 3)
                {
                    return PageWithError(null, nameof(PostText), "Mesajul este prea scurt (minim 3 caractere, exclusiv spații).");
                }

                var (Message, IsSuccess) = Action switch
                {
                    PostingActions.NewPrivateMessage => await _userService.SendPrivateMessage(user.UserId, user.Username, ReceiverId.Value, HttpUtility.HtmlEncode(PostTitle), await _writingService.PrepareTextForSaving(PostText), PageContext, HttpContext),
                    PostingActions.EditPrivateMessage => await _userService.EditPrivateMessage(PrivateMessageId.Value, HttpUtility.HtmlEncode(PostTitle), await _writingService.PrepareTextForSaving(PostText)),
                    _ => ("Unknown action", false)
                };

                return IsSuccess switch
                {
                    true => RedirectToPage("PrivateMessages", new { show = PrivateMessagesPages.Sent }),
                    _ => PageWithError(null, nameof(PostText), Message)
                };
            });

        public async Task<IActionResult> OnPostSaveDraft()
            => await WithRegisteredUser(async (user) => await WithValidForum(ForumId, async (curForum) => await WithNewestPostSincePageLoad(curForum, async () =>
            {
                if ((PostTitle?.Trim()?.Length ?? 0) < 3)
                {
                    return PageWithError(curForum, nameof(PostTitle), "Titlul este prea scurt (minim 3 caractere, exclusiv spații).");
                }

                if ((PostText?.Trim()?.Length ?? 0) < 3)
                {
                    return PageWithError(curForum, nameof(PostText), "Mesajul este prea scurt (minim 3 caractere, exclusiv spații).");
                }
                
                var conn = _context.Database.GetDbConnection();
                await conn.OpenIfNeededAsync();
                var topicId = Action == PostingActions.NewTopic ? 0 : TopicId ?? 0;
                var draft = conn.QueryFirstOrDefault<PhpbbDrafts>("SELECT * FROM phpbb_drafts WHERE user_id = @userId AND forum_id = @forumId AND topic_id = @topicId", new { user.UserId, ForumId, topicId });

                if (draft == null)
                {
                    await conn.ExecuteAsync(
                        "INSERT INTO phpbb_drafts (draft_message, draft_subject, forum_id, topic_id, user_id, save_time) VALUES (@message, @subject, @forumId, @topicId, @userId, @now)",
                        new { message = HttpUtility.HtmlEncode(PostText), subject = HttpUtility.HtmlEncode(PostTitle), ForumId, topicId = TopicId ?? 0, user.UserId, now = DateTime.UtcNow.ToUnixTimestamp() }
                    );
                }
                else
                {
                    await conn.ExecuteAsync(
                        "UPDATE phpbb_drafts SET draft_message = @message, draft_subject = @subject, save_time = @now WHERE draft_id = @draftId",
                        new { message = HttpUtility.HtmlEncode(PostText), subject = HttpUtility.HtmlEncode(PostTitle), now = DateTime.UtcNow.ToUnixTimestamp(), draft.DraftId }
                    );
                }
                await _cacheService.RemoveFromCache(await GetActualCacheKey("Text", true));
                DraftSavedSuccessfully = true;

                if (Action == PostingActions.NewForumPost)
                {
                    return await OnGetForumPost();
                }
                else if (Action == PostingActions.NewTopic)
                {
                    return await OnGetNewTopic();
                }
                else return RedirectToPage("Index");
            })));

        #endregion POST Message

        #region POST Poll

        public async Task<IActionResult> OnPostDeletePoll()
            => await WithBackup(async () => await WithRegisteredUser(async (user) => await WithValidPost(PostId ?? 0, async (curForum, curTopic, curPost) =>
            {
                if (!(await IsCurrentUserModeratorHere() || (curPost.PosterId == user.UserId && (user.PostEditTime == 0 || DateTime.UtcNow.Subtract(curPost.PostTime.ToUtcTime()).TotalMinutes <= user.PostEditTime))))
                {
                    return RedirectToPage("ViewTopic", "byPostId", new { PostId });
                }
                var connection = _context.Database.GetDbConnection();
                await connection.OpenAsync();

                await connection.ExecuteAsync("UPDATE phpbb_topics SET poll_start = 0, poll_length = 0, poll_max_options = 1, poll_title = '', poll_vote_change = 0 WHERE topic_id = @topicId", new { curTopic.TopicId });

                PollQuestion = PollOptions = null;
                PollCanChangeVote = false;
                PollMaxOptions = 1;
                PollExpirationDaysString = "1";
                ModelState.Remove(nameof(PollQuestion));
                ModelState.Remove(nameof(PollOptions));
                ModelState.Remove(nameof(PollExpirationDaysString));
                ModelState.Remove(nameof(PollCanChangeVote));
                ModelState.Remove(nameof(PollMaxOptions));

                await connection.ExecuteAsync(
                    "DELETE FROM phpbb_poll_options WHERE topic_id = @topicId;" +
                    "DELETE FROM phpbb_poll_votes WHERE topic_id = @topicId",
                    new { TopicId }
                );

                CurrentForum = curForum;
                CurrentTopic = curTopic;
                ForumId = curForum.ForumId;
                Action = PostingActions.EditForumPost;
                await Init();

                return Page();
            })));

        #endregion POST Poll

    }
}
