using Dapper;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;
using Newtonsoft.Json;
using PhpbbInDotnet.Database.Entities;
using PhpbbInDotnet.Domain;
using PhpbbInDotnet.Domain.Extensions;
using PhpbbInDotnet.Domain.Utilities;
using PhpbbInDotnet.Objects;
using PhpbbInDotnet.Objects.Configuration;
using PhpbbInDotnet.Objects.Messages;
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

        public async Task<IActionResult> OnGet()
        {
            var userAgent = Request.Headers.TryGetValue(HeaderNames.UserAgent, out var header) ? header.ToString() : "n/a";
            logger.Warning("Received call to default GET handler in Posting from user '{userName}' having user agent '{userAgent}' and query string '{queryString}'", 
                ForumUser.Username, userAgent, Request.QueryString);

            var cookie = (from kvp in Request.Cookies
                          where kvp.Key.StartsWith(CookieBackupKeyPrefix) && !string.IsNullOrWhiteSpace(kvp.Value)
                          let deserialized = suppressExceptions(() => JsonConvert.DeserializeObject<PostingBackup>(kvp.Value))
                          where deserialized is not null && (DateTime.UtcNow - deserialized.TextTime) < TimeSpan.FromMinutes(2)
                          orderby deserialized.TextTime descending
                          select deserialized)
                          .FirstOrDefault() ?? throw new InvalidOperationException("Attempted to retrieve latest possible cookie with posting backup, but did not found any.");

            Action = cookie.PostingActions;
            ForumId = cookie.ForumId != 0 ? cookie.ForumId : ForumId;
            TopicId ??= cookie.TopicId;
            PostId ??= cookie.PostId;
            QuotePostInDifferentTopic = cookie.QuotePostInDifferentTopic;

            return Action switch
            {
                PostingActions.NewTopic => await OnGetNewTopic(),
                PostingActions.NewForumPost when PostId > 0 => await OnGetQuoteForumPost(),
                PostingActions.NewForumPost when PostId is null => await OnGetForumPost(),
                PostingActions.EditForumPost => await OnGetEditPost(),
                _ => throw new InvalidOperationException($"Unsupported value '{Action}' for PostingActions.")
            };

            static T? suppressExceptions<T>(Func<T> toDo)
            {
                try
                {
                    return toDo();
                }
                catch 
                {
                    return default;
                }
            }
        }

        public Task<IActionResult> OnGetForumPost()
            => WithRegisteredUserAndCorrectPermissions((user) => WithValidTopic(TopicId ?? 0, async (curForum, curTopic) =>
            {
                CurrentForum = curForum;
                CurrentTopic = curTopic;
                ForumId = curForum.ForumId;
                Action = PostingActions.NewForumPost;
                ReturnUrl = Request.GetEncodedPathAndQuery();
                ExistingPostDraft = SqlExecuter.QueryFirstOrDefault<PhpbbDrafts>(
                    "SELECT * FROM phpbb_drafts WHERE user_id = @userId AND forum_id = @forumId AND topic_id = @topicId", 
                    new { user.UserId, ForumId, topicId = TopicId ?? 0 });
                if (ExistingPostDraft != null)
                {
                    PostTitle = HttpUtility.HtmlDecode(ExistingPostDraft.DraftSubject);
                    PostText = HttpUtility.HtmlDecode(ExistingPostDraft.DraftMessage);
                    Attachments = (await SqlExecuter.QueryAsync<PhpbbAttachments>(
                        "SELECT * FROM phpbb_attachments WHERE draft_id = @draftId ORDER BY order_in_post",
                        new { ExistingPostDraft.DraftId })).AsList();
                }
                else
                {
                    PostTitle = $"{Constants.REPLY}{HttpUtility.HtmlDecode(curTopic.TopicTitle)}";
                }
                await RestoreBackupIfAny(ExistingPostDraft?.SaveTime.ToUtcTime());
                ShowAttach = Attachments?.Count > 0;
                return Page();
            }));

        public Task<IActionResult> OnGetQuoteForumPost()
        {
            if (QuotePostInDifferentTopic)
            {
                TopicId = DestinationTopicId ?? 0;
                return WithRegisteredUserAndCorrectPermissions(_ => WithValidPost(PostId ?? 0, (_, _, curPost) => WithValidTopic(TopicId ?? 0, (curForum, curTopic) => toDo(curForum, curTopic, curPost))));
            }
            else
            {
                return WithRegisteredUserAndCorrectPermissions(_ => WithValidPost(PostId ?? 0, (curForum, curTopic, curPost) => toDo(curForum, curTopic, curPost)));
            }

            async Task<IActionResult> toDo(PhpbbForums curForum, PhpbbTopics curTopic, PhpbbPosts curPost)
            {
                QuotedPostAuthor = curPost.PostUsername;
                if (string.IsNullOrWhiteSpace(QuotedPostAuthor))
                {
                    QuotedPostAuthor = await SqlExecuter.QueryFirstOrDefaultAsync<string>("SELECT username FROM phpbb_users WHERE user_id = @posterId", new { curPost.PosterId }) ?? Constants.ANONYMOUS_USER_NAME;
                }

                CurrentForum = curForum;
                CurrentTopic = curTopic;
                ForumId = curForum.ForumId;
                Action = PostingActions.NewForumPost;
                ReturnUrl = Request.GetEncodedPathAndQuery();

                var dbQuotedAttachmentNames = await SqlExecuter.QueryAsync<string>(
                    "SELECT real_filename FROM phpbb_attachments WHERE post_msg_id = @postId ORDER BY order_in_post",
                    new { PostId });
                QuotedAttachments = dbQuotedAttachmentNames.Indexed().Select(a => new QuotedAttachment(a.Index, a.Item)).ToList();

                var title = HttpUtility.HtmlDecode(curPost.PostSubject);
                QuotedPostText = writingService.CleanBbTextForDisplay(curPost.PostText, curPost.BbcodeUid);
                PostTitle = title.StartsWith(Constants.REPLY) ? title : $"{Constants.REPLY}{title}";
                await RestoreBackupIfAny();
                ShowAttach = Attachments?.Count > 0;
                return Page();
            }
        }

        public Task<IActionResult> OnGetNewTopic()
            => WithRegisteredUserAndCorrectPermissions(user => WithValidForum(ForumId, async (curForum) =>
            {
                CurrentForum = curForum;
                CurrentTopic = null;
                Action = PostingActions.NewTopic;
                ReturnUrl = Request.GetEncodedPathAndQuery();

                ExistingPostDraft = SqlExecuter.QueryFirstOrDefault<PhpbbDrafts>(
                    "SELECT * FROM phpbb_drafts WHERE user_id = @userId AND forum_id = @forumId AND topic_id = @topicId", 
                    new { user.UserId, ForumId, topicId = 0 }); 
                if (ExistingPostDraft != null)
                {
                    PostTitle = HttpUtility.HtmlDecode(ExistingPostDraft.DraftSubject);
                    PostText = HttpUtility.HtmlDecode(ExistingPostDraft.DraftMessage);
                    Attachments = (await SqlExecuter.QueryAsync<PhpbbAttachments>(
                        "SELECT * FROM phpbb_attachments WHERE draft_id = @draftId ORDER BY order_in_post",
                        new { ExistingPostDraft.DraftId })).AsList();
                }
                await RestoreBackupIfAny(ExistingPostDraft?.SaveTime.ToUtcTime());
                ShowAttach = Attachments?.Count > 0;
                return Page();
            }));

        public Task<IActionResult> OnGetEditPost()
            => WithRegisteredUserAndCorrectPermissions(user => WithValidPost(PostId ?? 0, async (curForum, curTopic, curPost) =>
            {
                if (!(await UserService.IsUserModeratorInForum(ForumUser, ForumId) || (curPost.PosterId == user.UserId && (user.PostEditTime == 0 || DateTime.UtcNow.Subtract(curPost.PostTime.ToUtcTime()).TotalMinutes <= user.PostEditTime))))
                {
                    return RedirectToPage("ViewTopic", "byPostId", new { PostId });
                }

                var canCreatePoll = curTopic.TopicFirstPostId == PostId;

                CurrentForum = curForum;
                CurrentTopic = curTopic;
                ForumId = curForum.ForumId;
                Action = PostingActions.EditForumPost;
                ReturnUrl = Request.GetEncodedPathAndQuery();

                Attachments = (await SqlExecuter.QueryAsync<PhpbbAttachments>(
                    "SELECT * FROM phpbb_attachments WHERE post_msg_id = @postId ORDER BY order_in_post", 
                    new { PostId })).AsList();

                if (canCreatePoll && curTopic.PollStart > 0)
                {
                    var pollOptionsText = (await SqlExecuter.QueryAsync<string>("SELECT poll_option_text FROM phpbb_poll_options WHERE topic_id = @topicId", new { curTopic.TopicId })).AsList();
                    PollQuestion = curTopic.PollTitle;
                    PollOptions = string.Join(Environment.NewLine, pollOptionsText);
                    PollCanChangeVote = curTopic.PollVoteChange.ToBool();
                    PollExpirationDaysString = TimeSpan.FromSeconds(curTopic.PollLength).TotalDays.ToString();
                    PollMaxOptions = curTopic.PollMaxOptions;
                    ShowPoll = true;
                }

                var subject = curPost.PostSubject.StartsWith(Constants.REPLY) ? curPost.PostSubject[Constants.REPLY.Length..] : curPost.PostSubject;
                PostText = writingService.CleanBbTextForDisplay(curPost.PostText, curPost.BbcodeUid);
                PostTitle = HttpUtility.HtmlDecode(curPost.PostSubject);
                PostTime = curPost.PostTime;
                await RestoreBackupIfAny();
                ShowAttach = Attachments?.Count > 0;
                return Page();
            }));

        #endregion GET

        #region POST Attachment

        public Task<IActionResult> OnPostAddAttachment()
            => WithInitialBackup(() => WithRegisteredUserAndCorrectPermissions(user => WithValidForum(ForumId, async (curForum) =>
            {
                var lang = Language;
                CurrentForum = curForum;
                ShowAttach = true;
                var isMod = await UserService.IsUserModeratorInForum(ForumUser, ForumId);

                if (!(Files?.Any() ?? false))
                {
                    return Page();
                }

                if (!ShouldResize && !isMod)
                {
                    return PageWithError(curForum, nameof(Files), TranslationProvider.Errors[lang, "AN_ERROR_OCCURRED_TRY_AGAIN"]);
                }

                var sizeLimit = Configuration.GetObject<AttachmentLimits>("UploadLimitsMB");
                var countLimit = Configuration.GetObject<AttachmentLimits>("UploadLimitsCount");
                var images = Files.Where(f => StringUtility.IsImageMimeType(f.ContentType));
                var nonImages = Files.Where(f => !StringUtility.IsImageMimeType(f.ContentType));

                if (ShouldResize)
                {
                    try
                    {
                        images = await Task.WhenAll(images.Select(async image =>
                        {
                            var resultStream = await imageResizeService.ResizeImageByFileSize(image.OpenReadStream(), image.FileName, Constants.ONE_MB * sizeLimit.Images);
                            if (resultStream is null)
                            {
                                return image;
                            }
                            return new FormFile(resultStream, 0, resultStream.Length, image.Name, image.FileName)
                            {
                                Headers = image.Headers,
                                ContentType = image.ContentType,
                                ContentDisposition = image.ContentDisposition,
                            };
                        }));
                    }
                    catch (Exception ex)
                    {
                        logger.Warning(ex);
                    }
                }

                if (user.UploadLimit > 0)
                {
                    var existingUploadSize = await SqlExecuter.ExecuteScalarAsync<long>("SELECT sum(filesize) FROM phpbb_attachments WHERE poster_id = @userId", new { user.UserId });
                    if (existingUploadSize + images.Sum(f => f.Length) + nonImages.Sum(f => f.Length) > user.UploadLimit)
                    {
                        return PageWithError(curForum, nameof(Files), TranslationProvider.Errors[lang, "ATTACH_QUOTA_EXCEEDED"]);
                    }
                }

                var tooLargeFiles = images.Where(f => f.Length > Constants.ONE_MB * sizeLimit.Images).Union(nonImages.Where(f => f.Length > Constants.ONE_MB * sizeLimit.OtherFiles));
                if (tooLargeFiles.Any() && !isMod)
                {
                    return PageWithError(curForum, nameof(Files), string.Format(TranslationProvider.Errors[lang, "FILES_TOO_BIG_FORMAT"], string.Join(",", tooLargeFiles.Select(f => f.FileName))));
                }

                ReorderModelAttachmentsIfNeeded();
                var existingImages = Attachments?.Count(a => StringUtility.IsImageMimeType(a.Mimetype)) ?? 0;
                var existingNonImages = Attachments?.Count(a => !StringUtility.IsImageMimeType(a.Mimetype)) ?? 0;
                if (!isMod && (existingImages + images.Count() > countLimit.Images || existingNonImages + nonImages.Count() > countLimit.OtherFiles))
                {
                    return PageWithError(curForum, nameof(Files), TranslationProvider.Errors[lang, "TOO_MANY_FILES"]);
                }

                var minOrderInPost = (Attachments?.Max(attach => attach.OrderInPost) ?? 0) + 1;
                var (succeeded, failed) = await storageService.BulkAddAttachments(images.Union(nonImages), user.UserId, minOrderInPost);

                if (failed.Any())
                {
                    return PageWithError(curForum, nameof(Files), string.Format(TranslationProvider.Errors[lang, "GENERIC_ATTACH_ERROR_FORMAT"], string.Join(",", failed)));
                }

                if (Attachments == null)
                {
                    Attachments = succeeded.AsList();
                }
                else
                {
                    Attachments.AddRange(succeeded);
                }

                return BackedUpPage();
            }, ReturnUrl)));

        public Task<IActionResult> OnPostDeleteAttachment(int idToDelete)
            => WithInitialBackup(() => WithRegisteredUserAndCorrectPermissions(user => WithValidForum(ForumId, async (curForum) =>
            {
                CurrentForum = curForum;

                ReorderModelAttachmentsIfNeeded();
                if (await DeleteAttachment(idToDelete, true) is null)
                {
                    return PageWithError(curForum, nameof(DeleteFileDummyForValidation), TranslationProvider.Errors[Language, "CANT_DELETE_ATTACHMENT_TRY_AGAIN"]);
                }

                ShowAttach = Attachments?.Count > 0;
                ModelState.Clear();

                return BackedUpPage();
            }, ReturnUrl)));

        public Task<IActionResult> OnPostDeleteAllAttachments()
            => WithInitialBackup(() => WithRegisteredUserAndCorrectPermissions(user => WithValidForum(ForumId, async (curForum) =>
            {
                CurrentForum = curForum;

                var error = false;
                var successfullyDeleted = new HashSet<int>(Attachments?.Count ?? 0);
                foreach (var attach in Attachments ?? [])
                {
                    var deletedAttachment = await DeleteAttachment(attach.AttachId, false);
                    if (deletedAttachment is null)
                    {
                        error = true;
                        ModelState.AddModelError(nameof(DeleteFileDummyForValidation), TranslationProvider.Errors[Language, "CANT_DELETE_ATTACHMENT_TRY_AGAIN"]);
                    }
                    else
                    {
                        successfullyDeleted.Add(deletedAttachment.AttachId);
                    }
                }

                Attachments?.RemoveAll(attachment => successfullyDeleted.Contains(attachment.AttachId));
                ShowAttach = false;

                if (!error)
                {
                    ModelState.Clear();
                }

                return BackedUpPage();
            }, ReturnUrl)));

		#endregion POST Attachment

		#region POST Message

		public Task<IActionResult> OnPostPreview()
            => WithInitialBackup(() => WithRegisteredUserAndCorrectPermissions(user => WithValidForum(ForumId, curForum => WithNewestPostSincePageLoad(curForum, () => WithValidInput(curForum, async() =>
            {
                var lang = Language;
                var currentPost = Action == PostingActions.EditForumPost ? await SqlExecuter.QueryFirstOrDefaultAsync<PhpbbPosts>("SELECT * FROM phpbb_posts WHERE post_id = @PostId", new { PostId }) : null;
                var userId = Action == PostingActions.EditForumPost ? currentPost!.PosterId : user.UserId;
                var postAuthor = await SqlExecuter.QueryFirstOrDefaultAsync<PhpbbUsers>("SELECT * FROM phpbb_users WHERE user_id = @userId", new { userId });
                var rankId = postAuthor?.UserRank ?? 0;
                var newPostText = PostText?.Trim();
                var uid = string.Empty;
                newPostText = HttpUtility.HtmlEncode(newPostText);

                ReorderModelAttachmentsIfNeeded();
                var cacheResult = await postService.CacheAttachmentsAndPrepareForDisplay(Attachments!, ForumId, lang, 1, true);
                PreviewablePost = new PostDto
                {
                    Attachments = cacheResult.FirstOrDefault().Value ?? new List<AttachmentDto>(),
                    AuthorColor = postAuthor?.UserColour,
                    AuthorId = postAuthor?.UserId ?? Constants.ANONYMOUS_USER_ID,
                    AuthorName = postAuthor?.Username ?? Constants.ANONYMOUS_USER_NAME,
                    AuthorRank = (await SqlExecuter.QueryFirstOrDefaultAsync("SELECT * FROM phpbb_ranks WHERE rank_id = @rankId", new { rankId }))?.RankTitle,
                    BbcodeUid = uid,
                    PostEditCount = (short)(Action == PostingActions.EditForumPost ? (currentPost?.PostEditCount ?? 0) + 1 : 0),
                    PostEditReason = Action == PostingActions.EditForumPost ? currentPost?.PostEditReason : string.Empty,
                    PostEditTime = Action == PostingActions.EditForumPost ? DateTime.UtcNow.ToUnixTimestamp() : 0,
                    PostEditUser = Action == PostingActions.EditForumPost ? user.Username : string.Empty,
                    PostId = currentPost?.PostId ?? 0,
                    PostSubject = HttpUtility.HtmlEncode(PostTitle),
                    PostText = await writingService.PrepareTextForSaving(newPostText),
                    PostTime = currentPost?.PostTime ?? DateTime.UtcNow.ToUnixTimestamp()
                };

                if (!string.IsNullOrWhiteSpace(PollOptions))
                {
                    var topicId = currentPost?.TopicId ?? 0;
                    var curTopic = await SqlExecuter.QueryFirstOrDefaultAsync<PhpbbTopics>("SELECT * FROM phpbb_topics WHERE topic_id = @topicId", new { topicId });
                    var pollStart = ((curTopic?.PollStart ?? 0) == 0 ? DateTime.UtcNow.ToUnixTimestamp() : curTopic!.PollStart).ToUtcTime();
                    PreviewablePoll = new PollDto
                    {
                        PollTitle = HttpUtility.HtmlEncode(PollQuestion),
                        PollOptions = PollOptionsEnumerable.Select(x => new PollOption { PollOptionText = HttpUtility.HtmlEncode(x) }).ToList(),
                        VoteCanBeChanged = PollCanChangeVote,
                        PollDurationSecons = (int)TimeSpan.FromDays(double.TryParse(PollExpirationDaysString, out var val) ? val : 1d).TotalSeconds,
                        PollMaxOptions = PollMaxOptions ?? 1,
                        PollStart = pollStart
                    };
                }
                ShowAttach = Attachments?.Any() ?? false;
                CurrentForum = curForum;
                return Page();
            })), ReturnUrl)));

        public Task<IActionResult> OnPostNewForumPost()
            => WithInitialBackup(() => WithRegisteredUserAndCorrectPermissions(user => WithValidForum(ForumId, curForum => WithNewestPostSincePageLoad(curForum, () => WithValidInput(curForum, async () =>
            {
                await backgroundProcessingSession.SendMessage(new AddPostCommand { PostText = PostText });
                return PageWithError(curForum, nameof(PostText), "mesajul e postat");

                //ReorderModelAttachmentsIfNeeded();
                //var addedPostId = await UpsertPost(null, user);
                //if (addedPostId == null)
                //{
                //    return PageWithError(curForum, nameof(PostText), TranslationProvider.Errors[Language, "GENERIC_POSTING_ERROR"]);
                //}
                //return RedirectToPage("ViewTopic", "byPostId", new { postId = addedPostId });
            })), ReturnUrl)));

        public Task<IActionResult> OnPostEditForumPost()
            => WithInitialBackup(() => WithRegisteredUserAndCorrectPermissions(user => WithValidPost(PostId ?? 0, (curForum, curTopic, curPost) => WithValidInput(curForum, async() =>
            {
                if (!(await UserService.IsUserModeratorInForum(ForumUser, ForumId) || (curPost.PosterId == user.UserId && (user.PostEditTime == 0 || DateTime.UtcNow.Subtract(curPost.PostTime.ToUtcTime()).TotalMinutes <= user.PostEditTime))))
                {
                    return RedirectToPage("ViewTopic", "byPostId", new { PostId });
                }

                ReorderModelAttachmentsIfNeeded();
                var post = await SqlExecuter.QueryFirstOrDefaultAsync<PhpbbPosts>("SELECT * FROM phpbb_posts WHERE post_id = @PostId", new { PostId });
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
            }), ReturnUrl)));

		#endregion POST Message

		#region POST Draft

		public Task<IActionResult> OnPostSaveDraft()
            => WithInitialBackup(() => WithRegisteredUserAndCorrectPermissions(user => WithValidForum(ForumId, curForum => WithNewestPostSincePageLoad(curForum, () => WithValidInput(curForum, async() =>
            {
                try
                {
                    var topicId = Action switch
                    {
                        PostingActions.NewTopic => 0,
                        _ when QuotePostInDifferentTopic => DestinationTopicId,
                        _ => TopicId
                    };
                    
                    var draft = await SqlExecuter.QueryFirstOrDefaultAsync<PhpbbDrafts>(
                        "SELECT * FROM phpbb_drafts WHERE user_id = @userId AND forum_id = @forumId AND topic_id = @topicId",
                        new { user.UserId, ForumId, topicId });

                    if (draft == null)
                    {
                        draft = await SqlExecuter.QuerySingleAsync<PhpbbDrafts>(
                            "INSERT INTO phpbb_drafts (draft_message, draft_subject, forum_id, topic_id, user_id, save_time) VALUES (@message, @subject, @forumId, @topicId, @userId, @now);" +
                            $"SELECT * FROM phpbb_drafts WHERE draft_id = {SqlExecuter.LastInsertedItemId};",
                            new { message = HttpUtility.HtmlEncode(PostText), subject = HttpUtility.HtmlEncode(PostTitle), ForumId, topicId = TopicId ?? 0, user.UserId, now = DateTime.UtcNow.ToUnixTimestamp() });
                    }
                    else
                    {
                        var now = DateTime.UtcNow.ToUnixTimestamp();
                        await SqlExecuter.ExecuteAsync(
                            "UPDATE phpbb_drafts SET draft_message = @message, draft_subject = @subject, save_time = @now WHERE draft_id = @draftId",
                            new { message = HttpUtility.HtmlEncode(PostText), subject = HttpUtility.HtmlEncode(PostTitle), now, draft.DraftId });
                    }

                    ReorderModelAttachmentsIfNeeded();
                    foreach (var attach in Attachments!)
                    {
                        await SqlExecuter.ExecuteAsync(
                            @"UPDATE phpbb_attachments 
                                 SET draft_id = @draftId, attach_comment = @comment, is_orphan = 0, order_in_post = @orderInPost
                               WHERE attach_id = @attachId",
                        new
                        {
                            draft.DraftId,
                            comment = await writingService.PrepareTextForSaving(attach.AttachComment),
                            attach.AttachId,
                            attach.OrderInPost
                        });
                    }

                    SaveDraftMessage = TranslationProvider.BasicText[Language, "DRAFT_SAVED_SUCCESSFULLY"];
					SaveDraftSuccess = true;
                    Response.Cookies.DeleteObject(CookieBackupKey);

                    RemoveDraftFromModelState();
                }
                catch (Exception ex)
                {
					var id = logger.ErrorWithId(ex);
					SaveDraftMessage = string.Format(TranslationProvider.Errors[Language, "AN_ERROR_OCCURRED_TRY_AGAIN_ID_FORMAT"], id);
					SaveDraftSuccess = false;
				}

                return Action switch
                {
                    PostingActions.NewTopic => await OnGetNewTopic(),
                    PostingActions.NewForumPost when PostId > 0 => await OnGetQuoteForumPost(),
                    PostingActions.NewForumPost when PostId is null => await OnGetForumPost(),
                    PostingActions.EditForumPost => await OnGetEditPost(),
                    _ => RedirectToPage("Index")
                };
			})), ReturnUrl)));

        public Task<IActionResult> OnPostDeleteDraft()
            => WithInitialBackup(() => WithRegisteredUserAndCorrectPermissions(user => WithValidForum(ForumId, async curForum =>
            {
                try
                {
					await SqlExecuter.ExecuteAsync(
						"DELETE FROM phpbb_drafts WHERE draft_id = @draftId",
						new { ExistingPostDraft!.DraftId });
					
                    await ReorderModelAndDatabaseAttachmentsIfNeeded();

                    ExistingPostDraft = null;

					DeleteDraftMessage = TranslationProvider.BasicText[Language, "DRAFT_DELETED_SUCCESSFULLY"];
					DeleteDraftSuccess = true;

                    RemoveDraftFromModelState();
				}
                catch (Exception ex)
                {
                    var id = logger.ErrorWithId(ex);
                    DeleteDraftMessage = string.Format(TranslationProvider.Errors[Language, "AN_ERROR_OCCURRED_TRY_AGAIN_ID_FORMAT"], id);
                    DeleteDraftSuccess = false;
                }

                return Action switch
                {
                    PostingActions.NewTopic => await OnGetNewTopic(),
                    PostingActions.NewForumPost when PostId > 0 => await OnGetQuoteForumPost(),
                    PostingActions.NewForumPost when PostId is null => await OnGetForumPost(),
                    PostingActions.EditForumPost => await OnGetEditPost(),
                    _ => RedirectToPage("Index")
                };
            })));

		#endregion POST Draft

		#region POST Poll

		public async Task<IActionResult> OnPostDeletePoll()
            => await WithInitialBackup(() => WithRegisteredUserAndCorrectPermissions(user => WithValidPost(PostId ?? 0, async (curForum, curTopic, curPost) =>
            {
                if (!(await UserService.IsUserModeratorInForum(ForumUser, ForumId) || (curPost.PosterId == user.UserId && (user.PostEditTime == 0 || DateTime.UtcNow.Subtract(curPost.PostTime.ToUtcTime()).TotalMinutes <= user.PostEditTime))))
                {
                    return RedirectToPage("ViewTopic", "byPostId", new { PostId });
                }

                await SqlExecuter.ExecuteAsync("UPDATE phpbb_topics SET poll_start = 0, poll_length = 0, poll_max_options = 1, poll_title = '', poll_vote_change = 0 WHERE topic_id = @topicId", new { curTopic.TopicId });

                PollQuestion = PollOptions = null;
                PollCanChangeVote = false;
                PollMaxOptions = 1;
                PollExpirationDaysString = "1";
                ModelState.Remove(nameof(PollQuestion));
                ModelState.Remove(nameof(PollOptions));
                ModelState.Remove(nameof(PollExpirationDaysString));
                ModelState.Remove(nameof(PollCanChangeVote));
                ModelState.Remove(nameof(PollMaxOptions));

                await SqlExecuter.ExecuteAsync(
                    "DELETE FROM phpbb_poll_options WHERE topic_id = @topicId;" +
                    "DELETE FROM phpbb_poll_votes WHERE topic_id = @topicId",
                    new { TopicId }
                );

                await ReorderModelAndDatabaseAttachmentsIfNeeded();

                CurrentForum = curForum;
                CurrentTopic = curTopic;
                ForumId = curForum.ForumId;
                Action = PostingActions.EditForumPost;

                return Page();
            }, ReturnUrl)));

        #endregion POST Poll

    }
}
