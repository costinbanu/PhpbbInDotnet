using Dapper;
using LazyCache;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using PhpbbInDotnet.Database.Entities;
using PhpbbInDotnet.Domain;
using PhpbbInDotnet.Domain.Extensions;
using PhpbbInDotnet.Domain.Utilities;
using PhpbbInDotnet.Objects;
using PhpbbInDotnet.Objects.Configuration;
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

        public Task<IActionResult> OnGetForumPost()
            => WithRegisteredUserAndCorrectPermissions((user) => WithValidTopic(TopicId ?? 0, async (curForum, curTopic) =>
            {
                CurrentForum = curForum;
                CurrentTopic = curTopic;
                ForumId = curForum.ForumId;
                Action = PostingActions.NewForumPost;
                ReturnUrl = Request.GetEncodedPathAndQuery();
                var sqlExecuter = Context.GetSqlExecuter();
                var draft = sqlExecuter.QueryFirstOrDefault<PhpbbDrafts>("SELECT * FROM phpbb_drafts WHERE user_id = @userId AND forum_id = @forumId AND topic_id = @topicId", new { user.UserId, ForumId, topicId = TopicId ?? 0 });
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
                var curAuthor = curPost.PostUsername;
                if (string.IsNullOrWhiteSpace(curAuthor))
                {
                    var sqlExecuter = Context.GetSqlExecuter();
                    curAuthor = await sqlExecuter.QueryFirstOrDefaultAsync<string>("SELECT username FROM phpbb_users WHERE user_id = @posterId", new { curPost.PosterId }) ?? Constants.ANONYMOUS_USER_NAME;
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

        public Task<IActionResult> OnGetNewTopic()
            => WithRegisteredUserAndCorrectPermissions(user => WithValidForum(ForumId, async (curForum) =>
            {
                CurrentForum = curForum;
                CurrentTopic = null;
                Action = PostingActions.NewTopic;
                ReturnUrl = Request.GetEncodedPathAndQuery();

                var sqlExecuter = Context.GetSqlExecuter();
                var draft = sqlExecuter.QueryFirstOrDefault<PhpbbDrafts>("SELECT * FROM phpbb_drafts WHERE user_id = @userId AND forum_id = @forumId AND topic_id = @topicId", new { user.UserId, ForumId, topicId = 0 }); 
                if (draft != null)
                {
                    PostTitle = HttpUtility.HtmlDecode(draft.DraftSubject);
                    PostText = HttpUtility.HtmlDecode(draft.DraftMessage);
                }
                await RestoreBackupIfAny(draft?.SaveTime.ToUtcTime());
                ShowAttach = Attachments?.Any() == true;
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

                var sqlExecuter = Context.GetSqlExecuter();
                
                Attachments = (await sqlExecuter.QueryAsync<PhpbbAttachments>("SELECT * FROM phpbb_attachments WHERE post_msg_id = @postId ORDER BY attach_id", new { PostId })).AsList();

                Cache.Add(GetActualCacheKey("PostTime", true), curPost.PostTime, CACHE_EXPIRATION);

                if (canCreatePoll && curTopic.PollStart > 0)
                {
                    var pollOptionsText = (await sqlExecuter.QueryAsync<string>("SELECT poll_option_text FROM phpbb_poll_options WHERE topic_id = @topicId", new { curTopic.TopicId })).AsList();
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

        #endregion GET

        #region POST Attachment

        public Task<IActionResult> OnPostAddAttachment()
            => WithBackup(() => WithRegisteredUserAndCorrectPermissions(user => WithValidForum(ForumId, async (curForum) =>
            {
                var lang = Language;
                CurrentForum = curForum;
                ShowAttach = true;
                var isAdmin = await UserService.IsAdmin(ForumUser);

                if (!(Files?.Any() ?? false))
                {
                    return Page();
                }

                if (!ShouldResize && !isAdmin)
                {
                    return PageWithError(curForum, nameof(Files), TranslationProvider.Errors[lang, "AN_ERROR_OCCURRED_TRY_AGAIN"]);
                }

                var sizeLimit = _config.GetObject<AttachmentLimits>("UploadLimitsMB");
                var countLimit = _config.GetObject<AttachmentLimits>("UploadLimitsCount");
                var images = Files.Where(f => StringUtility.IsImageMimeType(f.ContentType));
                var nonImages = Files.Where(f => !StringUtility.IsImageMimeType(f.ContentType));

                if (_imageProcessorOptions.Api?.Enabled == true && (ShouldResize || ShouldHideLicensePlates))
                {
                    try
                    {
                        images = await Task.WhenAll(images.Select(async image =>
                        {
                            using var streamContent = new StreamContent(image.OpenReadStream());
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

                            var result = await _imageProcessorClient!.PostAsync(_imageProcessorOptions.Api.RelativeUri, formContent);

                            if (!result.IsSuccessStatusCode)
                            {
                                var errorMessage = await result.Content.ReadAsStringAsync();
                                throw new HttpRequestException($"The image processor API threw an exception: {errorMessage}");
                            }

                            var resultStream = await result.Content.ReadAsStreamAsync();
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
                        Logger.Warning(ex);
                    }
                }

                if (user.UploadLimit > 0)
                {
                    var existingUploadSize = await Context.GetSqlExecuter().ExecuteScalarAsync<long>("SELECT sum(filesize) FROM phpbb_attachments WHERE poster_id = @userId", new { user.UserId });
                    if (existingUploadSize + images.Sum(f => f.Length) + nonImages.Sum(f => f.Length) > user.UploadLimit)
                    {
                        return PageWithError(curForum, nameof(Files), TranslationProvider.Errors[lang, "ATTACH_QUOTA_EXCEEDED"]);
                    }
                }

                var tooLargeFiles = images.Where(f => f.Length > Constants.ONE_MB * sizeLimit.Images).Union(nonImages.Where(f => f.Length > Constants.ONE_MB * sizeLimit.OtherFiles));
                if (tooLargeFiles.Any() && !isAdmin)
                {
                    return PageWithError(curForum, nameof(Files), string.Format(TranslationProvider.Errors[lang, "FILES_TOO_BIG_FORMAT"], string.Join(",", tooLargeFiles.Select(f => f.FileName))));
                }

                var existingImages = Attachments?.Count(a => StringUtility.IsImageMimeType(a.Mimetype)) ?? 0;
                var existingNonImages = Attachments?.Count(a => !StringUtility.IsImageMimeType(a.Mimetype)) ?? 0;
                if (!isAdmin && (existingImages + images.Count() > countLimit.Images || existingNonImages + nonImages.Count() > countLimit.OtherFiles))
                {
                    return PageWithError(curForum, nameof(Files), TranslationProvider.Errors[lang, "TOO_MANY_FILES"]);
                }

                var (succeeded, failed) = await _storageService.BulkAddAttachments(images.Union(nonImages), user.UserId);

                if (failed.Any())
                {
                    return PageWithError(curForum, nameof(Files), string.Format(TranslationProvider.Errors[lang, "GENERIC_ATTACH_ERROR_FORMAT"], string.Join(",", failed)));
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

        public Task<IActionResult> OnPostDeleteAttachment(int index)
            => WithBackup(() => WithRegisteredUserAndCorrectPermissions(user => WithValidForum(ForumId, async (curForum) =>
            {
                CurrentForum = curForum;

                if (await DeleteAttachment(index, true) is null)
                {
                    return PageWithError(curForum, $"{nameof(DeleteFileDummyForValidation)}[{index}]", TranslationProvider.Errors[Language, "CANT_DELETE_ATTACHMENT_TRY_AGAIN"]);
                }

                ShowAttach = Attachments?.Any() == true;
                ModelState.Clear();

                return Page();
            }, ReturnUrl)));

        public Task<IActionResult> OnPostDeleteAllAttachments()
            => WithBackup(() => WithRegisteredUserAndCorrectPermissions(user => WithValidForum(ForumId, async (curForum) =>
            {
                CurrentForum = curForum;

                var error = false;
                var successfullyDeleted = new HashSet<int>(Attachments?.Count ?? 0);
                for (var index = 0; index < (Attachments?.Count ?? 0); index++)
                {
                    var deletedAttachment = await DeleteAttachment(index, false);
                    if (deletedAttachment is null)
                    {
                        error = true;
                        ModelState.AddModelError($"{nameof(DeleteFileDummyForValidation)}[{index}]", TranslationProvider.Errors[Language, "CANT_DELETE_ATTACHMENT_TRY_AGAIN"]);
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

                return Page();
            }, ReturnUrl)));

        #endregion POST Attachment

        #region POST Message

        public Task<IActionResult> OnPostPreview()
            => WithBackup(() => WithRegisteredUserAndCorrectPermissions(user => WithValidForum(ForumId, curForum => WithNewestPostSincePageLoad(curForum, () => WithValidInput(curForum, async() =>
            {
                var lang = Language;
                var sqlExecuter = Context.GetSqlExecuter();
                var currentPost = Action == PostingActions.EditForumPost ? await sqlExecuter.QueryFirstOrDefaultAsync<PhpbbPosts>("SELECT * FROM phpbb_posts WHERE post_id = @PostId", new { PostId }) : null;
                var userId = Action == PostingActions.EditForumPost ? currentPost!.PosterId : user.UserId;
                var postAuthor = await sqlExecuter.QueryFirstOrDefaultAsync<PhpbbUsers>("SELECT * FROM phpbb_users WHERE user_id = @userId", new { userId });
                var rankId = postAuthor?.UserRank ?? 0;
                var newPostText = PostText;
                var uid = string.Empty;
                newPostText = HttpUtility.HtmlEncode(newPostText);

                var cacheResult = await _postService.CacheAttachmentsAndPrepareForDisplay(Attachments!, ForumId, lang, 1, true);
                PreviewCorrelationId = cacheResult.CorrelationId;
                PreviewablePost = new PostDto
                {
                    Attachments = cacheResult.Attachments.FirstOrDefault().Value ?? new List<AttachmentDto>(),
                    AuthorColor = postAuthor?.UserColour,
                    AuthorId = postAuthor?.UserId ?? Constants.ANONYMOUS_USER_ID,
                    AuthorName = postAuthor?.Username ?? Constants.ANONYMOUS_USER_NAME,
                    AuthorRank = (await sqlExecuter.QueryFirstOrDefaultAsync("SELECT * FROM phpbb_ranks WHERE rank_id = @rankId", new { rankId }))?.RankTitle,
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
                    var curTopic = await Context.GetSqlExecuter().QueryFirstOrDefaultAsync<PhpbbTopics>("SELECT * FROM phpbb_topics WHERE topic_id = @topicId", new { topicId });
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
            => WithBackup(() => WithRegisteredUserAndCorrectPermissions(user => WithValidForum(ForumId, curForum => WithNewestPostSincePageLoad(curForum, () => WithValidInput(curForum, async () =>
            {
                var addedPostId = await UpsertPost(null, user);
                if (addedPostId == null)
                {
                    return PageWithError(curForum, nameof(PostText), TranslationProvider.Errors[Language, "GENERIC_POSTING_ERROR"]);
                }
                return RedirectToPage("ViewTopic", "byPostId", new { postId = addedPostId });
            })), ReturnUrl)));

        public Task<IActionResult> OnPostEditForumPost()
            => WithBackup(() => WithRegisteredUserAndCorrectPermissions(user => WithValidPost(PostId ?? 0, (curForum, curTopic, curPost) => WithValidInput(curForum, async() =>
            {
                if (!(await UserService.IsUserModeratorInForum(ForumUser, ForumId) || (curPost.PosterId == user.UserId && (user.PostEditTime == 0 || DateTime.UtcNow.Subtract(curPost.PostTime.ToUtcTime()).TotalMinutes <= user.PostEditTime))))
                {
                    return RedirectToPage("ViewTopic", "byPostId", new { PostId });
                }

                var post = await Context.GetSqlExecuter().QueryFirstOrDefaultAsync<PhpbbPosts>("SELECT * FROM phpbb_posts WHERE post_id = @PostId", new { PostId });
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


        public Task<IActionResult> OnPostSaveDraft()
            => WithRegisteredUserAndCorrectPermissions(user => WithValidForum(ForumId, curForum => WithNewestPostSincePageLoad(curForum, () => WithValidInput(curForum, async() =>
            {
                var lang = Language;
                var sqlExecuter = Context.GetSqlExecuter();
                var topicId = Action == PostingActions.NewTopic ? 0 : TopicId ?? 0;
                var draft = sqlExecuter.QueryFirstOrDefault<PhpbbDrafts>("SELECT * FROM phpbb_drafts WHERE user_id = @userId AND forum_id = @forumId AND topic_id = @topicId", new { user.UserId, ForumId, topicId });

                if (draft == null)
                {
                    await sqlExecuter.ExecuteAsync(
                        "INSERT INTO phpbb_drafts (draft_message, draft_subject, forum_id, topic_id, user_id, save_time) VALUES (@message, @subject, @forumId, @topicId, @userId, @now)",
                        new { message = HttpUtility.HtmlEncode(PostText), subject = HttpUtility.HtmlEncode(PostTitle), ForumId, topicId = TopicId ?? 0, user.UserId, now = DateTime.UtcNow.ToUnixTimestamp() }
                    );
                }
                else
                {
                    await sqlExecuter.ExecuteAsync(
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
            })), ReturnUrl));

        #endregion POST Message

        #region POST Poll

        public async Task<IActionResult> OnPostDeletePoll()
            => await WithBackup(() => WithRegisteredUserAndCorrectPermissions(user => WithValidPost(PostId ?? 0, async (curForum, curTopic, curPost) =>
            {
                if (!(await UserService.IsUserModeratorInForum(ForumUser, ForumId) || (curPost.PosterId == user.UserId && (user.PostEditTime == 0 || DateTime.UtcNow.Subtract(curPost.PostTime.ToUtcTime()).TotalMinutes <= user.PostEditTime))))
                {
                    return RedirectToPage("ViewTopic", "byPostId", new { PostId });
                }
                var sqlExecuter = Context.GetSqlExecuter();

                await sqlExecuter.ExecuteAsync("UPDATE phpbb_topics SET poll_start = 0, poll_length = 0, poll_max_options = 1, poll_title = '', poll_vote_change = 0 WHERE topic_id = @topicId", new { curTopic.TopicId });

                PollQuestion = PollOptions = null;
                PollCanChangeVote = false;
                PollMaxOptions = 1;
                PollExpirationDaysString = "1";
                ModelState.Remove(nameof(PollQuestion));
                ModelState.Remove(nameof(PollOptions));
                ModelState.Remove(nameof(PollExpirationDaysString));
                ModelState.Remove(nameof(PollCanChangeVote));
                ModelState.Remove(nameof(PollMaxOptions));

                await sqlExecuter.ExecuteAsync(
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
