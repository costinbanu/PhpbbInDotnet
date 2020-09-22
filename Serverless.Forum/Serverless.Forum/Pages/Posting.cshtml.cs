using Dapper;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Serverless.Forum.Contracts;
using Serverless.Forum.ForumDb;
using Serverless.Forum.ForumDb.Entities;
using Serverless.Forum.Pages.CustomPartials;
using Serverless.Forum.Pages.CustomPartials.Email;
using Serverless.Forum.Services;
using Serverless.Forum.Utilities;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Net.Mail;
using System.Threading.Tasks;
using System.Web;

namespace Serverless.Forum.Pages
{
    [ValidateAntiForgeryToken, ResponseCache(Location = ResponseCacheLocation.None, NoStore = true, Duration = 0)]
    public class PostingModel : ModelWithLoggedUser
    {
        [BindProperty, MaxLength(255, ErrorMessage = "Titlul trebuie să aibă maxim 255 caractere.")]
        public string PostTitle { get; set; }
        
        [BindProperty]
        public string PostText { get; set; }
        
        [BindProperty(SupportsGet = true)]
        public int ForumId { get; set; }
        
        [BindProperty(SupportsGet = true)]
        public int? TopicId { get; set; }

        [BindProperty(SupportsGet = true)]
        public int? PageNum { get; set; }

        [BindProperty(SupportsGet = true)]
        public int? PostId { get; set; }
        
        [BindProperty]
        public string PollQuestion { get; set; }
        
        [BindProperty]
        public string PollOptions { get; set; }
        
        [BindProperty]
        public string PollExpirationDaysString { get; set; }
        
        [BindProperty]
        public int? PollMaxOptions { get; set; }
        
        [BindProperty]
        public bool PollCanChangeVote { get; set; }
        
        [BindProperty]
        public IEnumerable<IFormFile> Files { get; set; }
        
        [BindProperty]
        public List<string> DeleteFileDummyForValidation { get; set; }

        [BindProperty]
        public string EditReason { get; set; }

        [BindProperty(SupportsGet = true)]
        public int? ReceiverId { get; set; }

        [BindProperty]
        public string ReceiverName { get; set; }

        [BindProperty(SupportsGet = true)]
        public int? PrivateMessageId { get; set; }

        [BindProperty]
        public List<PhpbbAttachments> Attachments { get; set; }

        [BindProperty]
        public long? PostTime { get; set; }

        [BindProperty]
        public PhpbbTopics CurrentTopic { get; set; }

        [BindProperty]
        public PostingActions Action { get; set; }

        [BindProperty]
        public long? LastPostTime { get; set; }
        
        public PostDto PreviewablePost { get; private set; }
        public PollDto PreviewablePoll { get; private set; }
        public bool ShowAttach { get; private set; } = false;
        public bool ShowPoll { get; private set; } = false;
        public PhpbbForums CurrentForum { get; private set; }

        private readonly PostService _postService;
        private readonly StorageService _storageService;
        private readonly WritingToolsService _writingService;
        private readonly BBCodeRenderingService _renderingService;
        private readonly Utils _utils;

        public PostingModel(Utils utils, ForumDbContext context, ForumTreeService forumService, UserService userService, CacheService cacheService,
            PostService postService, StorageService storageService, WritingToolsService writingService, BBCodeRenderingService renderingService, IConfiguration config)
            : base(context, forumService, userService, cacheService, config)
        {
            PollExpirationDaysString = "1";
            PollMaxOptions = 1;
            DeleteFileDummyForValidation = new List<string>();
            _utils = utils;
            _postService = postService;
            _storageService = storageService;
            _writingService = writingService;
            _renderingService = renderingService;
        }

        #region GET

        public async Task<IActionResult> OnGetForumPost()
            => await WithValidTopic(TopicId ?? 0, async (curForum, curTopic) =>
            {
                CurrentForum = curForum;
                CurrentTopic = curTopic;
                Action = PostingActions.NewForumPost;
                await Init();
                PostTitle = $"{Constants.REPLY}{HttpUtility.HtmlDecode(curTopic.TopicTitle)}";
                return Page();
            });

        public async Task<IActionResult> OnGetQuoteForumPost()
            => await WithValidPost(PostId ?? 0, async (curForum, curTopic, curPost) =>
            {
                var curAuthor = curPost.PostUsername;
                if (string.IsNullOrWhiteSpace(curAuthor))
                {
                    curAuthor = (await _context.PhpbbUsers.AsNoTracking().FirstOrDefaultAsync(u => u.UserId == curPost.PosterId))?.Username ?? "Anonymous";
                }

                CurrentForum = curForum;
                CurrentTopic = curTopic;
                Action = PostingActions.NewForumPost;
                await Init();

                var title = HttpUtility.HtmlDecode(curPost.PostSubject);
                PostText = $"[quote=\"{curAuthor}\"]\n{_writingService.CleanBbTextForDisplay(curPost.PostText, curPost.BbcodeUid)}\n[/quote]";
                PostTitle = title.StartsWith(Constants.REPLY) ? title : $"{Constants.REPLY}{title}";

                return Page();
            });

        public async Task<IActionResult> OnGetNewTopic()
            => await WithValidForum(ForumId, async (curForum) =>
            {
                CurrentForum = curForum;
                CurrentTopic = null;
                Action = PostingActions.NewTopic;
                await Init();
                return Page();
            });

        public async Task<IActionResult> OnGetEditPost()
            => await WithValidPost(PostId ?? 0, async (curForum, curTopic, curPost) =>
            {
                var CurrentUser = await GetCurrentUserAsync();
                if (!(await IsCurrentUserModeratorHere() || (curPost.PosterId == CurrentUser.UserId && (CurrentUser.PostEditTime == 0 || DateTime.UtcNow.Subtract(curPost.PostTime.ToUtcTime()).TotalMinutes <= CurrentUser.PostEditTime))))
                {
                    return RedirectToPage("ViewTopic", "byPostId", new { PostId });
                }

                var canCreatePoll = curTopic.TopicFirstPostId == PostId;

                CurrentForum = curForum;
                CurrentTopic = curTopic;
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

                return Page();
            });

        public async Task<IActionResult> OnGetPrivateMessage()
            => await WithRegisteredUser(async () =>
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

        #endregion GET

        #region POST Attachment

        public async Task<IActionResult> OnPostAddAttachment()
            => await WithRegisteredUser(async () => await WithValidForum(ForumId, async (curForum) =>
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

                 var (succeeded, failed) = await _storageService.BulkAddAttachments(Files, (await GetCurrentUserAsync()).UserId);

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
             }));

        public async Task<IActionResult> OnPostDeleteAttachment(int index)
            => await WithRegisteredUser(async () => await WithValidForum(ForumId, async (curForum) =>
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
            }));

        #endregion POST Attachment

        #region POST Message

        public async Task<IActionResult> OnPostPreview()
            => await WithRegisteredUser(async () => await WithValidForum(ForumId, Action == PostingActions.NewPrivateMessage, async (curForum) => await WithNewestPostSincePageLoad(curForum, async () => 
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
                var userId = Action == PostingActions.EditForumPost ? currentPost.PosterId : (await GetCurrentUserAsync()).UserId;
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
                    LastEditUser = Action == PostingActions.EditForumPost ? (await GetCurrentUserAsync()).Username : string.Empty,
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
        })));

        public async Task<IActionResult> OnPostNewForumPost()
            => await WithRegisteredUser(async () => await WithValidForum(ForumId, async (curForum) => await WithNewestPostSincePageLoad(curForum, async () =>
            {
                var addedPostId = await UpsertPost(null, await GetCurrentUserAsync());
                if (addedPostId == null)
                {
                    return PageWithError(curForum, nameof(PostText), "A intervenit o eroare iar mesajul nu a fost publicat. Te rugăm să încerci din nou.");
                }
                return RedirectToPage("ViewTopic", "byPostId", new { postId = addedPostId });
            })));

        public async Task<IActionResult> OnPostEditForumPost()
            => await WithRegisteredUser(async () => await WithValidForum(ForumId, async (_) =>
            {
                var addedPostId = await UpsertPost(await InitEditedPost(), await GetCurrentUserAsync());

                if (addedPostId == null)
                {
                    return Page();
                }
                return RedirectToPage("ViewTopic", "byPostId", new { postId = addedPostId });
            }));

        public async Task<IActionResult> OnPostPrivateMessage()
            => await WithRegisteredUser(async () =>
            {
                if ((ReceiverId ?? 1) == 1)
                {
                    ModelState.AddModelError(nameof(ReceiverName), "Introduceți un destinatar valid.");
                    return Page();
                }

                if ((PostTitle?.Trim()?.Length ?? 0) < 3)
                {
                    ModelState.AddModelError(nameof(ReceiverName), "Introduceți un subiect valid.");
                    return Page();
                }

                if ((PostText?.Trim()?.Length ?? 0) < 3)
                {
                    ModelState.AddModelError(nameof(ReceiverName), "Introduceți un mesaj valid.");
                    return Page();
                }

                var (Message, IsSuccess) = await _userService.SendPrivateMessage((await GetCurrentUserAsync()).UserId, ReceiverId.Value, HttpUtility.HtmlEncode(PostTitle), _writingService.PrepareTextForSaving(PostText));
                if (IsSuccess ?? false)
                {
                    try
                    {
                        using var emailMessage = new MailMessage
                        {
                            From = new MailAddress($"admin@metrouusor.com", _config.GetValue<string>("ForumName")),
                            Subject = $"Ai primit un mesaj privat nou pe {_config.GetValue<string>("ForumName")}",
                            Body = await _utils.RenderRazorViewToString(
                                "_NewPMEmailPartial",
                                new _NewPMEmailPartialModel
                                {
                                    SenderName = (await GetCurrentUserAsync()).Username
                                },
                                PageContext,
                                HttpContext
                            ),
                            IsBodyHtml = true
                        };
                        emailMessage.To.Add((await _context.PhpbbUsers.AsNoTracking().FirstOrDefaultAsync(u => u.UserId == ReceiverId.Value)).UserEmail);
                        await _utils.SendEmail(emailMessage);
                    }
                    catch (Exception ex)
                    {
                        _utils.HandleError(ex);
                    }
                    return RedirectToPage("PrivateMessages", new { show = PrivateMessagesPages.Sent });
                }
                else
                {
                    ModelState.AddModelError(nameof(ReceiverName), Message);
                    return Page();
                }
            });

        #endregion POST Message

        #region Helpers

        public async Task<string> GetActualCacheKey(string key, bool isPersonalizedData)
            => isPersonalizedData ? $"{(await GetCurrentUserAsync()).UserId}_{ForumId}_{TopicId ?? 0}_{key ?? throw new ArgumentNullException(nameof(key))}" : key;

        public async Task<(IEnumerable<PhpbbPosts> posts, IEnumerable<PhpbbUsers> users)> GetPreviousPosts()
        {
            if (((TopicId.HasValue && PageNum.HasValue) || PostId.HasValue) && (Action == PostingActions.EditForumPost || Action == PostingActions.NewForumPost))
            {
                using var connection = _context.Database.GetDbConnection();
                await connection.OpenIfNeeded();
                var posts = await connection.QueryAsync<PhpbbPosts>("SELECT * FROM phpbb_posts WHERE topic_id = @topicId ORDER BY post_time DESC LIMIT 10", new { TopicId });
                var users = await connection.QueryAsync<PhpbbUsers>("SELECT * FROM phpbb_users WHERE user_id IN @userIds", new { userIds = posts.Select(pp => pp.PosterId).DefaultIfEmpty() });
                return (posts, users);
            }
            return (new List<PhpbbPosts>(), new List<PhpbbUsers>());
        }

        private async Task Init()
        {
            using (var connection = _context.Database.GetDbConnection())
            {
                await connection.OpenIfNeeded();
                var smileys = await connection.QueryAsync<PhpbbSmilies>("SELECT * FROM phpbb_smilies GROUP BY smiley_url ORDER BY smiley_order");
                await _cacheService.SetInCache(await GetActualCacheKey("Smilies", false), smileys.ToList());
            }

            var userMap = await (
                from u in _context.PhpbbUsers.AsNoTracking()
                where u.UserId != Constants.ANONYMOUS_USER_ID && u.UserType != 2
                orderby u.Username
                select KeyValuePair.Create(u.Username, u.UserId)
            ).ToListAsync();
            await _cacheService.SetInCache(
                await GetActualCacheKey("Users", false),
                userMap.Select(map => KeyValuePair.Create(map.Key, $"[url={_config.GetValue<string>("BaseUrl")}/User?UserId={map.Value}]{map.Key}[/url]"))
            );
            await _cacheService.SetInCache(await GetActualCacheKey("UserMap", false), userMap);

            var dbBbCodes = await (
                from c in _context.PhpbbBbcodes.AsNoTracking()
                where c.DisplayOnPosting == 1
                select c
            ).ToListAsync();
            var helplines = new Dictionary<string, string>(Constants.BBCODE_HELPLINES);
            var bbcodes = new List<string>(Constants.BBCODES);
            foreach (var bbCode in dbBbCodes)
            {
                bbcodes.Add($"[{bbCode.BbcodeTag}]");
                bbcodes.Add($"[/{bbCode.BbcodeTag}]");
                var index = bbcodes.IndexOf($"[{bbCode.BbcodeTag}]");
                helplines.Add($"cb_{index}", bbCode.BbcodeHelpline);
            }
            await _cacheService.SetInCache(await GetActualCacheKey("BbCodeHelplines", false), helplines);
            await _cacheService.SetInCache(await GetActualCacheKey("BbCodes", false), bbcodes);
            await _cacheService.SetInCache(await GetActualCacheKey("DbBbCodes", false), dbBbCodes);
        }

        private async Task<PhpbbPosts> InitEditedPost()
        {
            var post = await _context.PhpbbPosts.FirstOrDefaultAsync(p => p.PostId == PostId);

            post.PostSubject = HttpUtility.HtmlEncode(PostTitle);
            post.PostEditTime = DateTime.UtcNow.ToUnixTimestamp();
            post.PostEditUser = (await GetCurrentUserAsync()).UserId;
            post.PostEditReason = HttpUtility.HtmlEncode(EditReason ?? string.Empty);
            post.PostEditCount++;

            return post;
        }

        private async Task<int?> UpsertPost(PhpbbPosts post, LoggedUser usr)
        {
            if ((PostTitle?.Trim()?.Length ?? 0) < 3)
            {
                ModelState.AddModelError(nameof(PostTitle), "Titlul este prea scurt (minim 3 caractere, exclusiv spații).");
                return null;
            }

            if ((PostText?.Trim()?.Length ?? 0) < 3)
            {
                ModelState.AddModelError(nameof(PostText), "Mesajul este prea scurt (minim 3 caractere, exclusiv spații).");
                return null;
            }

            var curTopic = Action != PostingActions.NewTopic ? await _context.PhpbbTopics.FirstOrDefaultAsync(t => t.TopicId == TopicId) : null;
            var canCreatePoll = Action == PostingActions.NewTopic || (Action == PostingActions.EditForumPost && (curTopic?.TopicFirstPostId ?? 0) == PostId);
            if (canCreatePoll && (string.IsNullOrWhiteSpace(PollExpirationDaysString) || !double.TryParse(PollExpirationDaysString, out var val) || val < 0 || val > 365))
            {
                ModelState.AddModelError(nameof(PollExpirationDaysString), "Valoarea introdusă nu este validă. Valori acceptate: între 0 și 365");
                ShowPoll = true;
                return null;
            }

            var pollOptionsArray = PollOptions?.Split(Environment.NewLine)?.Select(x => x.Trim()) ?? Enumerable.Empty<string>();
            if (canCreatePoll && (PollMaxOptions == null || (pollOptionsArray.Any() && (PollMaxOptions < 1 || PollMaxOptions > pollOptionsArray.Count()))))
            {
                ModelState.AddModelError(nameof(PollMaxOptions), "Valori valide: între 1 și numărul de opțiuni ale chestionarului.");
                ShowPoll = true;
                return null;
            }

            if (Action == PostingActions.NewTopic)
            {
                var topicResult = await _context.PhpbbTopics.AddAsync(new PhpbbTopics
                {
                    ForumId = ForumId,
                    TopicTitle = PostTitle,
                    TopicTime = DateTime.UtcNow.ToUnixTimestamp()
                });
                topicResult.Entity.TopicId = 0;
                await _context.SaveChangesAsync();
                curTopic = topicResult.Entity;
                TopicId = topicResult.Entity.TopicId;
            }

            var newPostText = PostText;
            var uid = string.Empty;
            var bitfield = string.Empty;
            var hasAttachments = Attachments?.Any() ?? false;
            if (_config.GetValue<bool>("CompatibilityMode"))
            {
                (newPostText, uid, bitfield) = _renderingService.TransformForBackwardsCompatibility(newPostText);
            }
            else
            {
                newPostText = HttpUtility.HtmlEncode(newPostText);
            }

            if (post == null)
            {
                var postResult = await _context.PhpbbPosts.AddAsync(new PhpbbPosts
                {
                    ForumId = ForumId,
                    TopicId = TopicId.Value,
                    PosterId = usr.UserId,
                    PostSubject = HttpUtility.HtmlEncode(PostTitle),
                    PostText = _writingService.PrepareTextForSaving(newPostText),
                    PostTime = DateTime.UtcNow.ToUnixTimestamp(),
                    PostApproved = 1,
                    PostReported = 0,
                    BbcodeUid = uid,
                    BbcodeBitfield = bitfield,
                    EnableBbcode = 1,
                    EnableMagicUrl = 1,
                    EnableSig = 1,
                    EnableSmilies = 1,
                    PostAttachment = (byte)(hasAttachments ? 1 : 0),
                    PostChecksum = _utils.CalculateMD5Hash(newPostText),
                    PostEditCount = 0,
                    PostEditLocked = 0,
                    PostEditReason = string.Empty,
                    PostEditTime = 0,
                    PostEditUser = 0,
                    PosterIp = HttpContext.Connection.RemoteIpAddress.ToString(),
                    PostUsername = HttpUtility.HtmlEncode(usr.Username)
                });
                postResult.Entity.PostId = 0;
                await _context.SaveChangesAsync();
                post = postResult.Entity;
                await _postService.CascadePostAdd(_context, post, false);
            }
            else
            {
                post.PostText = _writingService.PrepareTextForSaving(newPostText);
                post.BbcodeUid = uid;
                post.BbcodeBitfield = bitfield;
                post.PostAttachment = (byte)(hasAttachments ? 1 : 0);

                await _context.SaveChangesAsync();
                await _postService.CascadePostEdit(_context, post);
            }
            await _context.SaveChangesAsync();

            using var connection = _context.Database.GetDbConnection();
            await connection.OpenIfNeeded();
            var attachments = (await connection.QueryAsync<PhpbbAttachments>("SELECT * FROM phpbb_attachments WHERE attach_id IN @attachmentIds", new { attachmentIds = Attachments?.Select(a => a.AttachId).DefaultIfEmpty() })).AsList();
            _context.PhpbbAttachments.UpdateRange(attachments);
            for (var i = 0; i < (attachments?.Count ?? 0); i++)
            {
                attachments[i].PostMsgId = post.PostId;
                attachments[i].TopicId = TopicId.Value;
                attachments[i].AttachComment = _writingService.PrepareTextForSaving(Attachments?[i]?.AttachComment ?? string.Empty);
                attachments[i].IsOrphan = 0;
            }
            await _context.SaveChangesAsync();

            if (canCreatePoll && !string.IsNullOrWhiteSpace(PollOptions))
            {
                byte pollOptionId = 1;

                var options = await _context.PhpbbPollOptions.Where(o => o.TopicId == TopicId).ToListAsync();
                if (pollOptionsArray.Intersect(options.Select(x => x.PollOptionText.Trim()), StringComparer.InvariantCultureIgnoreCase).Count() != options.Count)
                {
                    _context.PhpbbPollOptions.RemoveRange(options);
                    _context.PhpbbPollVotes.RemoveRange(await _context.PhpbbPollVotes.Where(v => v.TopicId == TopicId).ToListAsync());
                    await _context.SaveChangesAsync();
                }

                foreach (var option in pollOptionsArray)
                {
                    await connection.ExecuteAsync(
                        "INSERT INTO forum.phpbb_poll_options (poll_option_id, topic_id, poll_option_text, poll_option_total) VALUES (@id, @topicId, @text, 0)",
                        new { id = pollOptionId++, TopicId, text = HttpUtility.HtmlEncode(option)  }
                    );
                }

                curTopic.PollStart = post.PostTime;
                curTopic.PollLength = (int)TimeSpan.FromDays(double.Parse(PollExpirationDaysString)).TotalSeconds;
                curTopic.PollMaxOptions = (byte)(PollMaxOptions ?? 1);
                curTopic.PollTitle = HttpUtility.HtmlEncode(PollQuestion);
                curTopic.PollVoteChange = (byte)(PollCanChangeVote ? 1 : 0);
            }

            await _context.SaveChangesAsync();
            await _cacheService.RemoveFromCache(await GetActualCacheKey("Header", true));

            return post.PostId;
        }

        private async Task<IActionResult> WithNewestPostSincePageLoad(PhpbbForums curForum, Func<Task<IActionResult>> toDo)
        {
            if ((TopicId ?? 0) > 0 && (LastPostTime ?? 0) > 0)
            {
                var currentLastPostTime = await _context.PhpbbPosts.AsNoTracking().Where(p => p.TopicId == TopicId).MaxAsync(p => p.PostTime);
                if (currentLastPostTime > LastPostTime)
                {
                    return PageWithError(curForum, nameof(LastPostTime), "De când a fost încărcată pagina, au mai fost scrie mesaje noi! Verifică mesajele precedente!");
                }
                else
                {
                    ModelState[nameof(LastPostTime)].Errors.Clear();
                }
            }
            return await toDo();
        }

        private IActionResult PageWithError(PhpbbForums curForum, string errorKey, string errorMessage)
        {
            ModelState.AddModelError(errorKey, errorMessage);
            CurrentForum = curForum;
            return Page();
        }

        #endregion Helpers
    }
}