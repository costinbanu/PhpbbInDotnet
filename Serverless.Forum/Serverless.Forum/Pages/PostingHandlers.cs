using Dapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Serverless.Forum.Contracts;
using Serverless.Forum.ForumDb.Entities;
using Serverless.Forum.Pages.CustomPartials;
using Serverless.Forum.Pages.CustomPartials.Email;
using Serverless.Forum.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mail;
using System.Threading.Tasks;
using System.Web;

namespace Serverless.Forum.Pages
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
                var draft = await _context.PhpbbDrafts.AsNoTracking().FirstOrDefaultAsync(d => d.UserId == user.UserId && d.ForumId == ForumId && d.TopicId == TopicId);
                if (draft != null)
                {
                    PostTitle = HttpUtility.HtmlDecode(draft.DraftSubject);
                    PostText = HttpUtility.HtmlDecode(draft.DraftMessage);
                }
                else
                {
                    PostTitle = $"{Constants.REPLY}{HttpUtility.HtmlDecode(curTopic.TopicTitle)}";
                }
                await RestoreCachedTextIfAny(draft?.SaveTime.ToUtcTime());
                return Page();
            }));

        public async Task<IActionResult> OnGetQuoteForumPost()
            => await WithRegisteredUser(async (user) => await WithValidPost(PostId ?? 0, async (curForum, curTopic, curPost) =>
            {
                var curAuthor = curPost.PostUsername;
                if (string.IsNullOrWhiteSpace(curAuthor))
                {
                    curAuthor = (await _context.PhpbbUsers.AsNoTracking().FirstOrDefaultAsync(u => u.UserId == curPost.PosterId))?.Username ?? "Anonymous";
                }

                CurrentForum = curForum;
                CurrentTopic = curTopic;
                ForumId = curForum.ForumId;
                Action = PostingActions.NewForumPost;
                await Init();

                var title = HttpUtility.HtmlDecode(curPost.PostSubject);
                PostText = $"[quote=\"{curAuthor}\"]\n{_writingService.CleanBbTextForDisplay(curPost.PostText, curPost.BbcodeUid)}\n[/quote]";
                PostTitle = title.StartsWith(Constants.REPLY) ? title : $"{Constants.REPLY}{title}";
                await RestoreCachedTextIfAny();
                return Page();
            }));

        public async Task<IActionResult> OnGetNewTopic()
            => await WithRegisteredUser(async (user) => await WithValidForum(ForumId, async (curForum) =>
            {
                CurrentForum = curForum;
                CurrentTopic = null;
                Action = PostingActions.NewTopic;
                await Init();
                var draft = await _context.PhpbbDrafts.AsNoTracking().FirstOrDefaultAsync(d => d.UserId == user.UserId && d.ForumId == ForumId && d.TopicId == 0);
                if (draft != null)
                {
                    PostTitle = HttpUtility.HtmlDecode(draft.DraftSubject);
                    PostText = HttpUtility.HtmlDecode(draft.DraftMessage);
                }
                await RestoreCachedTextIfAny(draft?.SaveTime.ToUtcTime());
                return Page();
            }));

        public async Task<IActionResult> OnGetEditPost()
            => await WithRegisteredUser(async (user) => await WithValidPost(PostId ?? 0, async (curForum, curTopic, curPost) =>
            {
                var CurrentUser = await GetCurrentUserAsync();
                if (!(await IsCurrentUserModeratorHere() || (curPost.PosterId == CurrentUser.UserId && (CurrentUser.PostEditTime == 0 || DateTime.UtcNow.Subtract(curPost.PostTime.ToUtcTime()).TotalMinutes <= CurrentUser.PostEditTime))))
                {
                    return RedirectToPage("ViewTopic", "byPostId", new { PostId });
                }

                var canCreatePoll = curTopic.TopicFirstPostId == PostId;

                CurrentForum = curForum;
                CurrentTopic = curTopic;
                ForumId = curForum.ForumId;
                Action = PostingActions.EditForumPost;
                await Init();

                Attachments = await _context.PhpbbAttachments.AsNoTracking().Where(a => a.PostMsgId == PostId).ToListAsync();
                ShowAttach = Attachments.Any();

                await _cacheService.SetInCache(await GetActualCacheKey("PostTime", true), curPost.PostTime);

                if (canCreatePoll && curTopic.PollStart > 0)
                {
                    var pollOptionsText = await _context.PhpbbPollOptions.AsNoTracking().Where(x => x.TopicId == curTopic.TopicId).Select(x => x.PollOptionText).ToListAsync();
                    PollQuestion = curTopic.PollTitle;
                    PollOptions = string.Join(Environment.NewLine, pollOptionsText);
                    PollCanChangeVote = curTopic.PollVoteChange == 1;
                    PollExpirationDaysString = TimeSpan.FromSeconds(curTopic.PollLength).TotalDays.ToString();
                    PollMaxOptions = curTopic.PollMaxOptions;
                }

                var subject = curPost.PostSubject.StartsWith(Constants.REPLY) ? curPost.PostSubject.Substring(Constants.REPLY.Length) : curPost.PostSubject;
                PostText = _writingService.CleanBbTextForDisplay(curPost.PostText, curPost.BbcodeUid);
                PostTitle = HttpUtility.HtmlDecode(curPost.PostSubject);
                PostTime = curPost.PostTime;
                await RestoreCachedTextIfAny();
                return Page();
            }));

        public async Task<IActionResult> OnGetPrivateMessage()
            => await WithRegisteredUser(async (usr) =>
            {
                if ((PostId ?? 0) > 0)
                {
                    var post = await _context.PhpbbPosts.AsNoTracking().FirstOrDefaultAsync(p => p.PostId == PostId);
                    if (post != null)
                    {
                        var author = await _context.PhpbbUsers.AsNoTracking().FirstOrDefaultAsync(u => u.UserId == post.PosterId);
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
                    var msg = await _context.PhpbbPrivmsgs.AsNoTracking().FirstOrDefaultAsync(p => p.MsgId == PrivateMessageId);
                    if (msg != null && ReceiverId == msg.AuthorId)
                    {
                        var author = await _context.PhpbbUsers.AsNoTracking().FirstOrDefaultAsync(u => u.UserId == ReceiverId);
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
                    ReceiverName = (await _context.PhpbbUsers.AsNoTracking().FirstOrDefaultAsync(u => u.UserId == ReceiverId)).Username;
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
                var pm = await _context.PhpbbPrivmsgs.AsNoTracking().FirstOrDefaultAsync(x => x.MsgId == PrivateMessageId);
                PostText = _writingService.CleanBbTextForDisplay(pm.MessageText, pm.BbcodeUid);
                PostTitle = HttpUtility.HtmlDecode(pm.MessageSubject);
                if ((ReceiverId ?? Constants.ANONYMOUS_USER_ID) != Constants.ANONYMOUS_USER_ID)
                {
                    ReceiverName = (await _context.PhpbbUsers.AsNoTracking().FirstOrDefaultAsync(u => u.UserId == ReceiverId)).Username;
                }
                Action = PostingActions.EditPrivateMessage;
                await Init();
                return Page();
            });

        #endregion GET

        #region POST Attachment

        public async Task<IActionResult> OnPostAddAttachment()
            => await WithCachedText(async () => await WithRegisteredUser(async (user) => await WithValidForum(ForumId, async (curForum) =>
            {
                CurrentForum = curForum;

                if (!(Files?.Any() ?? false))
                {
                    return Page();
                }

                var tooLargeFiles = Files.Where(f => f.Length > 1024 * 1024 * (f.ContentType.StartsWith("image/", StringComparison.InvariantCultureIgnoreCase) ? _config.GetValue<int>("UploadLimitsMB:Images") : _config.GetValue<int>("UploadLimitsMB:OtherFiles")));
                if (tooLargeFiles.Any() && !await IsCurrentUserAdminHere())
                {
                    ModelState.AddModelError(nameof(Files), $"Următoarele fișiere sunt prea mari: {string.Join(",", tooLargeFiles.Select(f => f.FileName))}");
                    ShowAttach = true;
                    return Page();
                }

                if ((Attachments?.Count ?? 0) + Files.Count() > 10 && !await IsCurrentUserAdminHere())
                {
                    ModelState.AddModelError(nameof(Files), "Sunt permise maxim 10 fișiere per mesaj.");
                    ShowAttach = true;
                    return Page();
                }

                var (succeeded, failed) = await _storageService.BulkAddAttachments(Files, user.UserId);

                if (failed.Any())
                {
                    ModelState.AddModelError(nameof(Files), $"Următoarele fișiere nu au putut fi adăugate, vă rugăm să încercați din nou: {string.Join(",", failed)}");
                }
                ShowAttach = true;

                if (Attachments == null)
                {
                    Attachments = succeeded;
                }
                else
                {
                    Attachments.AddRange(succeeded);
                }
                ModelState.Clear();
                return Page();
            })));

        public async Task<IActionResult> OnPostDeleteAttachment(int index)
            => await WithCachedText(async () => await WithRegisteredUser(async (user) => await WithValidForum(ForumId, async (curForum) =>
            {
                var attachment = Attachments?.ElementAtOrDefault(index);
                CurrentForum = curForum;

                if (attachment == null)
                {
                    ModelState.AddModelError($"{nameof(DeleteFileDummyForValidation)}[{index}]", "Fișierul nu a putut fi șters, vă rugăm încercați din nou.");
                    return Page();
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

                using var connection = _context.Database.GetDbConnection();
                await connection.OpenIfNeeded();
                await connection.ExecuteAsync("DELETE FROM phpbb_attachments WHERE attach_id = @attachId", new { attachment.AttachId });
                var dummy = Attachments.Remove(attachment);
                ShowAttach = Attachments.Any();
                ModelState.Clear();
                return Page();
            })));

        #endregion POST Attachment

        #region POST Message

        public async Task<IActionResult> OnPostPreview()
            => await WithCachedText(async () => await WithRegisteredUser(async (user) => await WithValidForum(ForumId, Action == PostingActions.NewPrivateMessage, async (curForum) => await WithNewestPostSincePageLoad(curForum, async () =>
            {
                if ((PostTitle?.Trim()?.Length ?? 0) < 3)
                {
                    return PageWithError(curForum, nameof(PostTitle), "Titlul este prea scurt (minim 3 caractere, exclusiv spații).");
                }

                if ((PostText?.Trim()?.Length ?? 0) < 3)
                {
                    return PageWithError(curForum, nameof(PostText), "Mesajul este prea scurt (minim 3 caractere, exclusiv spații).");
                }

                var currentPost = Action == PostingActions.EditForumPost ? await InitEditedPost() : null;
                var userId = Action == PostingActions.EditForumPost ? currentPost.PosterId : user.UserId;
                var postAuthor = await _context.PhpbbUsers.AsNoTracking().FirstOrDefaultAsync(x => x.UserId == userId);
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
                    Attachments = Attachments?.Select(a => new _AttachmentPartialModel(a.RealFilename, a.AttachComment, 0, a.Mimetype, 0, a.Filesize, a.PhysicalFilename, true))?.ToList() ?? new List<_AttachmentPartialModel>(),
                    AuthorColor = postAuthor.UserColour,
                    AuthorHasAvatar = !string.IsNullOrWhiteSpace(postAuthor?.UserAvatar),
                    AuthorId = postAuthor.UserId,
                    AuthorName = postAuthor.Username,
                    AuthorRank = (await _context.PhpbbRanks.AsNoTracking().FirstOrDefaultAsync(x => x.RankId == rankId))?.RankTitle,
                    BbcodeUid = uid,
                    PostCreationTime = Action == PostingActions.EditForumPost ? PostTime?.ToUtcTime() : DateTime.UtcNow,
                    EditCount = (short)(Action == PostingActions.EditForumPost ? (currentPost?.PostEditCount ?? 0) + 1 : 0),
                    LastEditReason = Action == PostingActions.EditForumPost ? currentPost?.PostEditReason : string.Empty,
                    LastEditTime = Action == PostingActions.EditForumPost ? DateTime.UtcNow.ToUnixTimestamp() : 0,
                    LastEditUser = Action == PostingActions.EditForumPost ? user.Username : string.Empty,
                    PostId = currentPost?.PostId ?? 0,
                    PostSubject = HttpUtility.HtmlEncode(PostTitle),
                    PostText = _writingService.PrepareTextForSaving(newPostText)
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
            => await WithRegisteredUser(async (user) => await WithValidForum(ForumId, async (_) =>
            {
                var addedPostId = await UpsertPost(await InitEditedPost(), user);

                if (addedPostId == null)
                {
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
                    PostingActions.NewPrivateMessage => await _userService.SendPrivateMessage(user.UserId, user.Username, ReceiverId.Value, HttpUtility.HtmlEncode(PostTitle), _writingService.PrepareTextForSaving(PostText), PageContext, HttpContext),
                    PostingActions.EditPrivateMessage => await _userService.EditPrivateMessage(PrivateMessageId.Value, HttpUtility.HtmlEncode(PostTitle), _writingService.PrepareTextForSaving(PostText)),
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

                var topicId = Action == PostingActions.NewTopic ? 0 : TopicId ?? 0;
                var draft = await _context.PhpbbDrafts.FirstOrDefaultAsync(d => d.UserId == user.UserId && d.ForumId == ForumId && d.TopicId == topicId);

                if (draft == null)
                {
                    await _context.PhpbbDrafts.AddAsync(new PhpbbDrafts
                    {
                        DraftMessage = HttpUtility.HtmlEncode(PostText),
                        DraftSubject = HttpUtility.HtmlEncode(PostTitle),
                        ForumId = ForumId,
                        TopicId = TopicId ?? 0,
                        UserId = user.UserId,
                        SaveTime = DateTime.UtcNow.ToUnixTimestamp()
                    });
                }
                else
                {
                    draft.DraftMessage = HttpUtility.HtmlEncode(PostText);
                    draft.DraftSubject = HttpUtility.HtmlEncode(PostTitle);
                    draft.SaveTime = DateTime.UtcNow.ToUnixTimestamp();
                }
                await _context.SaveChangesAsync();
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
                else return RedirectToPage("/");
            })));

        #endregion POST Message

    }
}
