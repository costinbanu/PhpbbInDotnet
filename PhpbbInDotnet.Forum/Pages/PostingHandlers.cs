using Dapper;
using LazyCache;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using PhpbbInDotnet.Database.Entities;
using PhpbbInDotnet.Objects;
using PhpbbInDotnet.Objects.Configuration;
using PhpbbInDotnet.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web;

namespace PhpbbInDotnet.Forum.Pages
{
    public partial class PostingModel
    {
        #region GET

        public async Task<IActionResult> OnGetForumPost()
            => await WithRegisteredUser((user) => WithValidTopic(TopicId ?? 0, async (curForum, curTopic) =>
            {
                CurrentForum = curForum;
                CurrentTopic = curTopic;
                ForumId = curForum.ForumId;
                Action = PostingActions.NewForumPost;
                ReturnUrl = Request.GetEncodedPathAndQuery();
                var conn = await Context.GetDbConnectionAsync();
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
                ShowAttach = Attachments?.Any() == true;
                return Page();
            }));

        public async Task<IActionResult> OnGetQuoteForumPost()
        {
            if (QuotePostInDifferentTopic)
            {
                TopicId = DestinationTopicId ?? 0;
                return await WithRegisteredUser(_ => WithValidPost(PostId ?? 0, (_, _, curPost) => WithValidTopic(TopicId ?? 0, (curForum, curTopic) => toDo(curForum, curTopic, curPost))));
            }
            else
            {
                return await WithRegisteredUser(_ => WithValidPost(PostId ?? 0, (curForum, curTopic, curPost) => toDo(curForum, curTopic, curPost)));
            }

            async Task<IActionResult> toDo(PhpbbForums curForum, PhpbbTopics curTopic, PhpbbPosts curPost)
            {
                var curAuthor = curPost.PostUsername;
                if (string.IsNullOrWhiteSpace(curAuthor))
                {
                    var conn = await Context.GetDbConnectionAsync();
                    curAuthor = await conn.QueryFirstOrDefaultAsync<string>("SELECT username FROM phpbb_users WHERE user_id = @posterId", new { curPost.PosterId }) ?? Constants.ANONYMOUS_USER_NAME;
                }

                CurrentForum = curForum;
                CurrentTopic = curTopic;
                ForumId = curForum.ForumId;
                Action = PostingActions.NewForumPost;
                ReturnUrl = Request.GetEncodedPathAndQuery();

                var title = HttpUtility.HtmlDecode(curPost.PostSubject);
                PostText = $"[quote=\"{curAuthor}\",{PostId}]\n{_writingService.CleanBbTextForDisplay(curPost.PostText, curPost.BbcodeUid)}\n[/quote]\n";
                PostTitle = title.StartsWith(Constants.REPLY) ? title : $"{Constants.REPLY}{title}";
                await RestoreBackupIfAny();
                ShowAttach = Attachments?.Any() == true;
                return Page();
            }
        }

        public async Task<IActionResult> OnGetNewTopic()
            => await WithRegisteredUser(user => WithValidForum(ForumId, async (curForum) =>
            {
                CurrentForum = curForum;
                CurrentTopic = null;
                Action = PostingActions.NewTopic;
                ReturnUrl = Request.GetEncodedPathAndQuery();

                var conn = await Context.GetDbConnectionAsync();
                var draft = conn.QueryFirstOrDefault<PhpbbDrafts>("SELECT * FROM phpbb_drafts WHERE user_id = @userId AND forum_id = @forumId AND topic_id = @topicId", new { user.UserId, ForumId, topicId = 0 }); 
                if (draft != null)
                {
                    PostTitle = HttpUtility.HtmlDecode(draft.DraftSubject);
                    PostText = HttpUtility.HtmlDecode(draft.DraftMessage);
                }
                await RestoreBackupIfAny(draft?.SaveTime.ToUtcTime());
                ShowAttach = Attachments?.Any() == true;
                return Page();
            }));

        public async Task<IActionResult> OnGetEditPost()
            => await WithRegisteredUser(user => WithValidPost(PostId ?? 0, async (curForum, curTopic, curPost) =>
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
                ReturnUrl = Request.GetEncodedPathAndQuery();

                var conn = await Context.GetDbConnectionAsync();
                
                Attachments = (await conn.QueryAsync<PhpbbAttachments>("SELECT * FROM phpbb_attachments WHERE post_msg_id = @postId ORDER BY attach_id", new { PostId })).AsList();

                Cache.Add(GetActualCacheKey("PostTime", true), curPost.PostTime, CACHE_EXPIRATION);

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

                var subject = curPost.PostSubject.StartsWith(Constants.REPLY) ? curPost.PostSubject[Constants.REPLY.Length..] : curPost.PostSubject;
                PostText = _writingService.CleanBbTextForDisplay(curPost.PostText, curPost.BbcodeUid);
                PostTitle = HttpUtility.HtmlDecode(curPost.PostSubject);
                PostTime = curPost.PostTime;
                await RestoreBackupIfAny();
                ShowAttach = Attachments?.Any() == true;
                return Page();
            }));

        public async Task<IActionResult> OnGetPrivateMessage()
            => await WithRegisteredUser(async (usr) =>
            {
                var lang = GetLanguage();
                var conn = await Context.GetDbConnectionAsync();
                
                if ((PostId ?? 0) > 0)
                {
                    var post = await conn.QueryFirstOrDefaultAsync<PhpbbPosts>("SELECT * FROM phpbb_posts WHERE post_id = @postId", new { PostId });
                    if (post != null)
                    {
                        var author = await conn.QueryFirstOrDefaultAsync<PhpbbUsers>("SELECT * FROM phpbb_users WHERE user_id = @posterId", new { post.PosterId });
                        if ((author?.UserId ?? Constants.ANONYMOUS_USER_ID) != Constants.ANONYMOUS_USER_ID)
                        {
                            PostTitle = HttpUtility.HtmlDecode(post.PostSubject);
                            PostText = $"[quote]\n{_writingService.CleanBbTextForDisplay(post.PostText, post.BbcodeUid)}\n[/quote]\n[url={_config.GetValue<string>("BaseUrl").Trim('/')}/ViewTopic?postId={PostId}&handler=byPostId]{PostTitle}[/url]\n";
                            ReceiverId = author.UserId;
                            ReceiverName = author.Username;
                        }
                        else
                        {
                            return RedirectToPage("Error", new { CustomErrorMessage = await Utils.CompressAndEncode(LanguageProvider.Errors[lang, "RECEIVER_DOESNT_EXIST"]) });
                        }
                    }
                    else
                    {
                        return RedirectToPage("Error", new { CustomErrorMessage = await Utils.CompressAndEncode(LanguageProvider.Errors[lang, "POST_DOESNT_EXIST"]) });
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
                            PostText = $"[quote]\n{_writingService.CleanBbTextForDisplay(msg.MessageText, msg.BbcodeUid)}\n[/quote]\n";
                            ReceiverName = author.Username;
                        }
                        else
                        {
                            return RedirectToPage("Error", new { CustomErrorMessage = await Utils.CompressAndEncode(LanguageProvider.Errors[lang, "RECEIVER_DOESNT_EXIST"]) });
                        }
                    }
                    else
                    {
                        return RedirectToPage("Error", new { CustomErrorMessage = await Utils.CompressAndEncode(LanguageProvider.Errors[lang, "PM_DOESNT_EXIST"]) });
                    }
                }
                else if ((ReceiverId ?? Constants.ANONYMOUS_USER_ID) != Constants.ANONYMOUS_USER_ID)
                {
                    ReceiverName = (await conn.QueryFirstOrDefaultAsync<PhpbbUsers>("SELECT * FROM phpbb_users WHERE user_id = @receiverId", new { ReceiverId }))?.Username;
                }

                CurrentForum = null;
                CurrentTopic = null;
                Action = PostingActions.NewPrivateMessage;
                ReturnUrl = Request.GetEncodedPathAndQuery();

                return Page();
            });

        public async Task<IActionResult> OnGetEditPrivateMessage()
            => await WithRegisteredUser(async (user) =>
            {
                var conn = await Context.GetDbConnectionAsync();

                var pm = await conn.QueryFirstOrDefaultAsync<PhpbbPrivmsgs>("SELECT * FROM phpbb_privmsgs WHERE msg_id = @privateMessageId", new { PrivateMessageId });
                PostText = _writingService.CleanBbTextForDisplay(pm.MessageText, pm.BbcodeUid);
                PostTitle = HttpUtility.HtmlDecode(pm.MessageSubject);
                if ((ReceiverId ?? Constants.ANONYMOUS_USER_ID) != Constants.ANONYMOUS_USER_ID)
                {
                    ReceiverName = (await conn.QueryFirstOrDefaultAsync<PhpbbUsers>("SELECT * FROM phpbb_users WHERE user_id = @receiverId", new { ReceiverId }))?.Username;
                }
                Action = PostingActions.EditPrivateMessage;
                ReturnUrl = Request.GetEncodedPathAndQuery();

                return Page();
            });

        #endregion GET

        #region POST Attachment

        public async Task<IActionResult> OnPostAddAttachment()
            => await WithBackup(() => WithRegisteredUser(user => WithValidForum(ForumId, async (curForum) =>
            {
                var lang = GetLanguage();
                CurrentForum = curForum;
                ShowAttach = true;
                var isAdmin = await IsCurrentUserAdminHere();

                if (!(Files?.Any() ?? false))
                {
                    return Page();
                }

                if (!ShouldResize && !isAdmin)
                {
                    return PageWithError(curForum, nameof(Files), LanguageProvider.Errors[lang, "AN_ERROR_OCCURRED_TRY_AGAIN"]);
                }

                var sizeLimit = _config.GetObject<AttachmentLimits>("UploadLimitsMB");
                var countLimit = _config.GetObject<AttachmentLimits>("UploadLimitsCount");
                var images = Files.Where(f => f.ContentType.IsImageMimeType());
                var nonImages = Files.Where(f => !f.ContentType.IsImageMimeType());

                if (_imageProcessorOptions.Api.Enabled && (ShouldResize || ShouldHideLicensePlates))
                {
                    images = await Task.WhenAll(images.Select(async image =>
                    {
                        var streamContent = new StreamContent(image.OpenReadStream());
                        streamContent.Headers.Add("Content-Type", image.ContentType);
                        using var formContent = new MultipartFormDataContent
                        {
                            { streamContent, "File", image.FileName },
                            { new StringContent(ShouldHideLicensePlates.ToString()), "HideLicensePlates" },
                        };
                        if (ShouldResize)
                        {
                            formContent.Add(new StringContent((Constants.ONE_MB * sizeLimit.Images).ToString()), "SizeLimit");
                        }

                        var result = await _imageProcessorClient.PostAsync(_imageProcessorOptions.Api.RelativeUri, formContent);
                        result.EnsureSuccessStatusCode();

                        var resultStream = await result.Content.ReadAsStreamAsync();
                        return new FormFile(resultStream, 0, resultStream.Length, image.Name, image.FileName)
                        {
                            Headers = image.Headers,
                            ContentType = image.ContentType,
                            ContentDisposition = image.ContentDisposition,
                        };
                    }));
                }

                if ((user.UploadLimit ?? 0) > 0)
                {
                    var existingUploadSize = await Context.PhpbbAttachments.AsNoTracking().Where(a => a.PosterId == user.UserId).SumAsync(a => a.Filesize);
                    if (existingUploadSize + images.Sum(f => f.Length) + nonImages.Sum(f => f.Length) > user.UploadLimit)
                    {
                        return PageWithError(curForum, nameof(Files), LanguageProvider.Errors[lang, "ATTACH_QUOTA_EXCEEDED"]);
                    }
                }

                var tooLargeFiles = images.Where(f => f.Length > Constants.ONE_MB * sizeLimit.Images).Union(nonImages.Where(f => f.Length > Constants.ONE_MB * sizeLimit.OtherFiles));
                if (tooLargeFiles.Any() && !isAdmin)
                {
                    return PageWithError(curForum, nameof(Files), string.Format(LanguageProvider.Errors[lang, "FILES_TOO_BIG_FORMAT"], string.Join(",", tooLargeFiles.Select(f => f.FileName))));
                }

                var existingImages = Attachments?.Count(a => a.Mimetype.IsImageMimeType()) ?? 0;
                var existingNonImages = Attachments?.Count(a => !a.Mimetype.IsImageMimeType()) ?? 0;
                if (!isAdmin && (existingImages + images.Count() > countLimit.Images || existingNonImages + nonImages.Count() > countLimit.OtherFiles))
                {
                    return PageWithError(curForum, nameof(Files), LanguageProvider.Errors[lang, "TOO_MANY_FILES"]);
                }

                var (succeeded, failed) = await _storageService.BulkAddAttachments(images.Union(nonImages), user.UserId);

                if (failed.Any())
                {
                    return PageWithError(curForum, nameof(Files), string.Format(LanguageProvider.Errors[lang, "GENERIC_ATTACH_ERROR_FORMAT"], string.Join(",", failed)));
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
            }, ReturnUrl)));

        public async Task<IActionResult> OnPostDeleteAttachment(int index)
            => await WithBackup(() => WithRegisteredUser(user => WithValidForum(ForumId, async (curForum) =>
            {
                var lang = GetLanguage();
                var attachment = Attachments?.ElementAtOrDefault(index);
                CurrentForum = curForum;

                if (attachment == null)
                {
                    return PageWithError(curForum, $"{nameof(DeleteFileDummyForValidation)}[{index}]", LanguageProvider.Errors[lang, "CANT_DELETE_ATTACHMENT_TRY_AGAIN"]);
                }

                if (!_storageService.DeleteFile(attachment.PhysicalFilename, false))
                {
                    ModelState.AddModelError($"{nameof(DeleteFileDummyForValidation)}[{index}]", LanguageProvider.Errors[lang, "CANT_DELETE_ATTACHMENT_TRY_AGAIN"]);
                }

                if (!string.IsNullOrWhiteSpace(PostText))
                {
                    PostText = PostText.Replace($"[attachment={index}]{attachment.RealFilename}[/attachment]", string.Empty, StringComparison.InvariantCultureIgnoreCase);
                    for (int i = index + 1; i < Attachments.Count; i++)
                    {
                        PostText = PostText.Replace($"[attachment={i}]{Attachments[i].RealFilename}[/attachment]", $"[attachment={i - 1}]{Attachments[i].RealFilename}[/attachment]", StringComparison.InvariantCultureIgnoreCase);
                    }
                }

                var connection = await Context.GetDbConnectionAsync();
                await connection.ExecuteAsync("DELETE FROM phpbb_attachments WHERE attach_id = @attachId", new { attachment.AttachId });
                var dummy = Attachments.Remove(attachment);
                ShowAttach = Attachments?.Any() == true;
                ModelState.Clear();
                return Page();
            }, ReturnUrl)));

        #endregion POST Attachment

        #region POST Message

        public async Task<IActionResult> OnPostPreview()
            => await WithBackup(() => WithRegisteredUser(user => WithValidForum(ForumId, Action == PostingActions.NewPrivateMessage, curForum => WithNewestPostSincePageLoad(curForum, async () =>
            {
                var lang = GetLanguage();
                if ((PostTitle?.Trim()?.Length ?? 0) < 3)
                {
                    return PageWithError(curForum, nameof(PostTitle), LanguageProvider.Errors[lang, "TITLE_TOO_SHORT"]);
                }

                if ((PostText?.Trim()?.Length ?? 0) < 3)
                {
                    return PageWithError(curForum, nameof(PostText), LanguageProvider.Errors[lang, "POST_TOO_SHORT"]);
                }

                var conn = await Context.GetDbConnectionAsync();

                var currentPost = Action == PostingActions.EditForumPost ? await conn.QueryFirstOrDefaultAsync<PhpbbPosts>("SELECT * FROM phpbb_posts WHERE post_id = @PostId", new { PostId }) : null;
                var userId = Action == PostingActions.EditForumPost ? currentPost.PosterId : user.UserId;
                var postAuthor = await conn.QueryFirstOrDefaultAsync<PhpbbUsers>("SELECT * FROM phpbb_users WHERE user_id = @userId", new { userId });
                var rankId = postAuthor?.UserRank ?? 0;
                var newPostText = PostText;
                var uid = string.Empty;
                newPostText = HttpUtility.HtmlEncode(newPostText);

                var cacheResult = await _postService.CacheAttachmentsAndPrepareForDisplay(Attachments, lang, 1, true);
                PreviewCorrelationId = cacheResult.CorrelationId;
                PreviewablePost = new PostDto
                {
                    Attachments = cacheResult.Attachments.FirstOrDefault().Value ?? new List<AttachmentDto>(),
                    AuthorColor = postAuthor.UserColour,
                    AuthorId = postAuthor.UserId,
                    AuthorName = postAuthor.Username,
                    AuthorRank = (await conn.QueryFirstOrDefaultAsync("SELECT * FROM phpbb_ranks WHERE rank_id = @rankId", new { rankId }))?.RankTitle,
                    BbcodeUid = uid,
                    PostEditCount = (short)(Action == PostingActions.EditForumPost ? (currentPost?.PostEditCount ?? 0) + 1 : 0),
                    PostEditReason = Action == PostingActions.EditForumPost ? currentPost?.PostEditReason : string.Empty,
                    PostEditTime = Action == PostingActions.EditForumPost ? DateTime.UtcNow.ToUnixTimestamp() : 0,
                    PostEditUser = Action == PostingActions.EditForumPost ? user.Username : string.Empty,
                    PostId = currentPost?.PostId ?? 0,
                    PostSubject = HttpUtility.HtmlEncode(PostTitle),
                    PostText = await _writingService.PrepareTextForSaving(newPostText),
                    PostTime = currentPost?.PostTime ?? DateTime.UtcNow.ToUnixTimestamp()
                };

                if (!string.IsNullOrWhiteSpace(PollOptions))
                {
                    var topicId = currentPost?.TopicId ?? 0;
                    var curTopic = await Context.PhpbbTopics.AsNoTracking().FirstOrDefaultAsync(t => t.TopicId == topicId);
                    var pollStart = ((curTopic?.PollStart ?? 0) == 0 ? DateTime.UtcNow.ToUnixTimestamp() : curTopic.PollStart).ToUtcTime();
                    PreviewablePoll = new PollDto
                    {
                        PollTitle = HttpUtility.HtmlEncode(PollQuestion),
                        PollOptions = new List<PollOption>(GetPollOptionsEnumerable().Select(x => new PollOption { PollOptionText = HttpUtility.HtmlEncode(x) })),
                        VoteCanBeChanged = PollCanChangeVote,
                        PollDurationSecons = (int)TimeSpan.FromDays(double.Parse(PollExpirationDaysString)).TotalSeconds,
                        PollMaxOptions = PollMaxOptions ?? 1,
                        PollStart = pollStart
                    };
                }
                ShowAttach = Attachments?.Any() ?? false;
                CurrentForum = curForum;
                return Page();
            }), ReturnUrl)));

        public async Task<IActionResult> OnPostNewForumPost()
            => await WithBackup(() => WithRegisteredUser(user => WithValidForum(ForumId, curForum => WithNewestPostSincePageLoad(curForum, async () =>
            {
                var addedPostId = await UpsertPost(null, user);
                if (addedPostId == null)
                {
                    return PageWithError(curForum, nameof(PostText), LanguageProvider.Errors[GetLanguage(), "GENERIC_POSTING_ERROR"]);
                }
                return RedirectToPage("ViewTopic", "byPostId", new { postId = addedPostId });
            }), ReturnUrl)));

        public async Task<IActionResult> OnPostEditForumPost()
            => await WithBackup(() => WithRegisteredUser(user => WithValidPost(PostId ?? 0, async (curForum, curTopic, curPost) =>
            {
                if (!(await IsCurrentUserModeratorHere() || (curPost.PosterId == user.UserId && (user.PostEditTime == 0 || DateTime.UtcNow.Subtract(curPost.PostTime.ToUtcTime()).TotalMinutes <= user.PostEditTime))))
                {
                    return RedirectToPage("ViewTopic", "byPostId", new { PostId });
                }

                var post = await (await Context.GetDbConnectionAsync()).QueryFirstOrDefaultAsync<PhpbbPosts>("SELECT * FROM phpbb_posts WHERE post_id = @PostId", new { PostId });
                var addedPostId = await UpsertPost(post, user);

                if (addedPostId == null)
                {
                    CurrentForum = curForum;
                    CurrentTopic = curTopic;
                    ForumId = curForum.ForumId;
                    Action = PostingActions.EditForumPost;
                    return Page();
                }
                return RedirectToPage("ViewTopic", "byPostId", new { postId = addedPostId });
            }, ReturnUrl)));

        public async Task<IActionResult> OnPostPrivateMessage()
            => await WithRegisteredUser(async (user) =>
            {
                var lang = GetLanguage();

                if ((ReceiverId ?? 1) == 1)
                {
                    return PageWithError(null, nameof(ReceiverName), LanguageProvider.Errors[lang, "ENTER_VALID_RECEIVER"]);
                }

                if ((PostTitle?.Trim()?.Length ?? 0) < 3)
                {
                    return PageWithError(null, nameof(PostTitle), LanguageProvider.Errors[lang, "TITLE_TOO_SHORT"]);
                }

                if ((PostTitle?.Length ?? 0) > 255)
                {
                    return PageWithError(null, nameof(PostTitle), LanguageProvider.Errors[lang, "TITLE_TOO_LONG"]);
                }

                if ((PostText?.Trim()?.Length ?? 0) < 3)
                {
                    return PageWithError(null, nameof(PostText), LanguageProvider.Errors[lang, "POST_TOO_SHORT"]);
                }

                var (Message, IsSuccess) = Action switch
                {
                    PostingActions.NewPrivateMessage => await UserService.SendPrivateMessage(user.UserId, user.Username, ReceiverId.Value, HttpUtility.HtmlEncode(PostTitle), await _writingService.PrepareTextForSaving(PostText), PageContext, HttpContext),
                    PostingActions.EditPrivateMessage => await UserService.EditPrivateMessage(PrivateMessageId.Value, HttpUtility.HtmlEncode(PostTitle), await _writingService.PrepareTextForSaving(PostText)),
                    _ => ("Unknown action", false)
                };

                return IsSuccess switch
                {
                    true => RedirectToPage("PrivateMessages", new { show = PrivateMessagesPages.Sent }),
                    _ => PageWithError(null, nameof(PostText), Message)
                };
            });

        public async Task<IActionResult> OnPostSaveDraft()
            => await WithRegisteredUser(user => WithValidForum(ForumId, curForum => WithNewestPostSincePageLoad(curForum, async () =>
            {
                var lang = GetLanguage();

                if ((PostTitle?.Trim()?.Length ?? 0) < 3)
                {
                    return PageWithError(curForum, nameof(PostTitle), LanguageProvider.Errors[lang, "TITLE_TOO_SHORT"]);
                }

                if ((PostTitle?.Length ?? 0) > 255)
                {
                    return PageWithError(curForum, nameof(PostTitle), LanguageProvider.Errors[lang, "TITLE_TOO_LONG"]);
                }

                if ((PostText?.Trim()?.Length ?? 0) < 3)
                {
                    return PageWithError(curForum, nameof(PostText), LanguageProvider.Errors[lang, "POST_TOO_SHORT"]);
                }

                var conn = await Context.GetDbConnectionAsync();
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
                Cache.Remove(GetActualCacheKey("Text", true));
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
            }), ReturnUrl));

        #endregion POST Message

        #region POST Poll

        public async Task<IActionResult> OnPostDeletePoll()
            => await WithBackup(() => WithRegisteredUser(user => WithValidPost(PostId ?? 0, async (curForum, curTopic, curPost) =>
            {
                if (!(await IsCurrentUserModeratorHere() || (curPost.PosterId == user.UserId && (user.PostEditTime == 0 || DateTime.UtcNow.Subtract(curPost.PostTime.ToUtcTime()).TotalMinutes <= user.PostEditTime))))
                {
                    return RedirectToPage("ViewTopic", "byPostId", new { PostId });
                }
                var connection = await Context.GetDbConnectionAsync();

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

                return Page();
            }, ReturnUrl)));

        #endregion POST Poll

    }
}
