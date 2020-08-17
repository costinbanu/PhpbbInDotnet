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
        public List<string> FileComment { get; set; }
        
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
        
        public PostDto PreviewablePost { get; private set; }
        
        public PollDto PreviewablePoll { get; private set; }
        
        public bool ShowAttach { get; private set; } = false;
        
        public bool ShowPoll { get; private set; } = false;
        
        private readonly PostService _postService;
        private readonly StorageService _storageService;
        private readonly WritingToolsService _writingService;
        private readonly BBCodeRenderingService _renderingService;
        private readonly IConfiguration _config;
        private readonly Utils _utils;

        public PostingModel(Utils utils, ForumDbContext context, ForumTreeService forumService, UserService userService, CacheService cacheService,
            PostService postService, StorageService storageService, WritingToolsService writingService, BBCodeRenderingService renderingService, IConfiguration config)
            : base(context, forumService, userService, cacheService)
        {
            PollExpirationDaysString = "1";
            PollMaxOptions = 1;
            FileComment = new List<string>();
            DeleteFileDummyForValidation = new List<string>();
            _utils = utils;
            _postService = postService;
            _storageService = storageService;
            _writingService = writingService;
            _renderingService = renderingService;
            _config = config;
        }

        #region GET

        public async Task<IActionResult> OnGetForumPost()
            => await WithValidTopic(TopicId ?? 0, async (curForum, curTopic) =>
            {
                await Init(PostingActions.NewForumPost, false, HttpUtility.HtmlDecode(curTopic.TopicTitle), curForum);
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

                await Init(PostingActions.NewForumPost, false, HttpUtility.HtmlDecode(curTopic.TopicTitle), curForum);

                var title = HttpUtility.HtmlDecode(curPost.PostSubject);
                PostText = $"[quote=\"{curAuthor}\"]\n{HttpUtility.HtmlDecode(_writingService.CleanBbTextForDisplay(curPost.PostText, curPost.BbcodeUid))}\n[/quote]";
                PostTitle = title.StartsWith(Constants.REPLY) ? title : $"{Constants.REPLY}{title}";

                return Page();
            });

        public async Task<IActionResult> OnGetNewTopic()
            => await WithValidForum(ForumId, async (curForum) =>
            {
                await Init(PostingActions.NewTopic, true, HttpUtility.HtmlDecode(curForum.ForumName), curForum);
                return Page();
            });

        public async Task<IActionResult> OnGetEditPost()
            => await WithValidPost(PostId ?? 0, async (curForum, curTopic, curPost) =>
            {
                //de testat asta
                if (!await IsCurrentUserModeratorHereAsync() && DateTime.UtcNow.Subtract(curPost.PostTime.ToUtcTime()).TotalMinutes > (await GetCurrentUserAsync()).PostEditTime)
                {
                    return RedirectToPage("ViewTopic", "byPostId", new { PostId });
                }

                var canCreatePoll = (curTopic.TopicFirstPostId == PostId) && (await IsCurrentUserModeratorHereAsync() || (curPost.PosterId == (await GetCurrentUserAsync()).UserId && DateTime.UtcNow.Subtract(curPost.PostTime.ToUtcTime()).TotalMinutes <= (await GetCurrentUserAsync()).PostEditTime));

                await Init(PostingActions.EditForumPost, canCreatePoll, HttpUtility.HtmlDecode(curTopic.TopicTitle), curForum);

                var attachments = await _context.PhpbbAttachments.AsNoTracking().Where(a => a.PostMsgId == PostId).ToListAsync();
                await _cacheService.SetInCache(await GetActualCacheKey("PostAttachments", true), attachments);
                ShowAttach = attachments.Any();
                FileComment = attachments.Select(a => HttpUtility.HtmlDecode(_writingService.CleanBbTextForDisplay(a.AttachComment, string.Empty))).ToList();

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
                PostText = HttpUtility.HtmlDecode(_writingService.CleanBbTextForDisplay(curPost.PostText, curPost.BbcodeUid));
                PostTitle = HttpUtility.HtmlDecode(curPost.PostSubject);

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
                            PostText = $"[quote]{HttpUtility.HtmlDecode(_writingService.CleanBbTextForDisplay(post.PostText, post.BbcodeUid))}[/quote]\r\n[url=./ViewTopic?postId={PostId}&handler=byPostId]{PostTitle}[/url]";
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
                            PostText = $"[quote]{ HttpUtility.HtmlDecode(_writingService.CleanBbTextForDisplay(msg.MessageText, msg.BbcodeUid))}[/quote]";
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

                await Init(PostingActions.NewPrivateMessage, false, null, null);
                return Page();
            });

        #endregion GET

        #region POST Attachment

        public async Task<IActionResult> OnPostAddAttachment()
            => await WithRegisteredUser(async () => await WithValidForum(ForumId, async (_) =>
             {
                 if (!(Files?.Any() ?? false))
                 {
                     return Page();
                 }

                 var tooLargeFiles = Files.Where(f => f.Length > 1024 * 1024 * 2);
                 if (tooLargeFiles.Any() && !await IsCurrentUserAdminHereAsync())
                 {
                     ModelState.AddModelError(nameof(Files), $"Următoarele fișiere sunt mai mari de 2MB: {string.Join(",", tooLargeFiles.Select(f => f.FileName))}");
                     ShowAttach = true;
                     return Page();
                 }

                 var attachList = (await _cacheService.GetFromCache<List<PhpbbAttachments>>(await GetActualCacheKey("PostAttachments", true))) ?? new List<PhpbbAttachments>();
                 if (attachList.Count + Files.Count() > 10 && !await IsCurrentUserAdminHereAsync())
                 {
                     ModelState.AddModelError(nameof(Files), "Sunt permise maxim 10 fișiere per mesaj.");
                     ShowAttach = true;
                     return Page();
                 }

                 var (succeeded, failed) = await _storageService.BulkAddAttachments(Files, (await GetCurrentUserAsync()).UserId);
                 attachList.AddRange(succeeded);

                 if (failed.Any())
                 {
                     ModelState.AddModelError(nameof(Files), $"Următoarele fișiere nu au putut fi adăugate, vă rugăm să încercați din nou: {string.Join(",", failed)}");
                 }
                 ShowAttach = true;
                 if (FileComment?.Any() ?? false)
                 {
                     FileComment = Enumerable.Repeat(string.Empty, attachList.Count).ToList();
                 }
                 else
                 {
                     FileComment.AddRange(Enumerable.Repeat(string.Empty, Files.Count()));
                 }

                 await _cacheService.SetInCache(await GetActualCacheKey("PostAttachments", true), attachList);
                 return Page();
             }));

        public async Task<IActionResult> OnPostDeleteAttachment(int index)
            => await WithRegisteredUser(async () => await WithValidForum(ForumId, async (_) =>
            {
                var attachList = (await _cacheService.GetFromCache<List<PhpbbAttachments>>(await GetActualCacheKey("PostAttachments", true))) ?? new List<PhpbbAttachments>();
                var attachment = attachList.ElementAtOrDefault(index);

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
                    for (int i = index + 1; i < attachList.Count; i++)
                    {
                        PostText = PostText.Replace($"[attachment={i}]{attachList[i].RealFilename}[/attachment]", $"[attachment={i - 1}]{attachList[i].RealFilename}[/attachment]", StringComparison.InvariantCultureIgnoreCase);
                    }
                    ModelState.Remove(nameof(PostText));
                }

                var dbAttach = await _context.PhpbbAttachments.FirstOrDefaultAsync(a => a.AttachId == attachList[index].AttachId);
                if (dbAttach != null)
                {
                    _context.PhpbbAttachments.Remove(dbAttach);
                    await _context.SaveChangesAsync();
                }
                ShowAttach = true;

                attachList.RemoveAt(index);
                await _cacheService.SetInCache(await GetActualCacheKey("PostAttachments", true), attachList);
                return Page();
            }));

        #endregion POST Attachment

        #region POST Message

        public async Task<IActionResult> OnPostPreview()
            => await WithRegisteredUser(async () =>
            {
                var action = await _cacheService.GetFromCache<PostingActions>(await GetActualCacheKey("Action", true));
                return await WithValidForum(ForumId, action == PostingActions.NewPrivateMessage, async (_) =>
                {
                    if ((PostTitle?.Trim()?.Length ?? 0) < 3)
                    {
                        ModelState.AddModelError(nameof(PostTitle), "Titlul este prea scurt (minim 3 caractere, exclusiv spații).");
                        return Page();
                    }

                    if ((PostText?.Trim()?.Length ?? 0) < 3)
                    {
                        ModelState.AddModelError(nameof(PostText), "Mesajul este prea scurt (minim 3 caractere, exclusiv spații).");
                        return Page();
                    }

                    var currentPost = action == PostingActions.EditForumPost ? await InitEditedPost() : null;
                    var userId = action == PostingActions.EditForumPost ? currentPost.PosterId : (await GetCurrentUserAsync()).UserId;
                    var postAuthor = await _context.PhpbbUsers.AsNoTracking().FirstOrDefaultAsync(x => x.UserId == userId);
                    var rankId = postAuthor?.UserRank ?? 0;
                    var attachments = await _cacheService.GetFromCache<List<PhpbbAttachments>>(await GetActualCacheKey("PostAttachments", true)) ?? new List<PhpbbAttachments>();
                    var postTime = action == PostingActions.EditForumPost ? (await _cacheService.GetFromCache<long?>(await GetActualCacheKey("PostTime", true)))?.ToUtcTime() : DateTime.UtcNow;

                    PreviewablePost = new PostDto
                    {
                        Attachments = new List<_AttachmentPartialModel>(),
                        AuthorColor = postAuthor.UserColour,
                        AuthorHasAvatar = !string.IsNullOrWhiteSpace(postAuthor?.UserAvatar),
                        AuthorId = postAuthor.UserId,
                        AuthorName = postAuthor.Username,
                        AuthorRank = (await _context.PhpbbRanks.AsNoTracking().FirstOrDefaultAsync(x => x.RankId == rankId))?.RankTitle,
                        BbcodeUid = _utils.NewBbcodeUid(),
                        PostCreationTime = postTime,
                        EditCount = (short)(action == PostingActions.EditForumPost ? (currentPost?.PostEditCount ?? 0) + 1 : 0),
                        LastEditReason = action == PostingActions.EditForumPost ? currentPost?.PostEditReason : string.Empty,
                        LastEditTime = action == PostingActions.EditForumPost ? DateTime.UtcNow.ToUnixTimestamp() : 0,
                        LastEditUser = action == PostingActions.EditForumPost ? (await GetCurrentUserAsync()).Username : string.Empty,
                        PostId = currentPost?.PostId ?? 0,
                        PostSubject = HttpUtility.HtmlEncode(PostTitle),
                        PostText = _writingService.PrepareTextForSaving(HttpUtility.HtmlEncode(PostText))
                    };

                    for (var i = 0; i < attachments.Count; i++)
                    {
                        attachments[i].AttachComment = FileComment[i];
                        PreviewablePost.Attachments.Add(new _AttachmentPartialModel(attachments[i], true));
                    }

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
                    ShowAttach = attachments.Any();

                    return Page();
                });
            });

        public async Task<IActionResult> OnPostNewForumPost()
            => await WithRegisteredUser(async () => await WithValidForum(ForumId, async (_) =>
            {
                var addedPostId = await UpsertPost(null, await GetCurrentUserAsync());

                if (addedPostId == null)
                {
                    return Page();
                }

                return RedirectToPage("ViewTopic", "byPostId", new { postId = addedPostId });
            }));

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

                var (Message, IsSuccess) = await _userService.SendPrivateMessage((await GetCurrentUserAsync()).UserId, ReceiverId.Value, HttpUtility.HtmlEncode(PostTitle), _writingService.PrepareTextForSaving(HttpUtility.HtmlEncode(PostText)));
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

        public async Task<(List<PhpbbPosts> posts, List<PhpbbUsers> users)> GetPreviousPosts(PostingActions action)
        {
            if(((TopicId.HasValue && PageNum.HasValue) || PostId.HasValue) && (action == PostingActions.EditForumPost || action == PostingActions.NewForumPost))
            {
                var (previousPosts, _, _) = await _postService.GetPostPageAsync((await GetCurrentUserAsync()).UserId, TopicId, PageNum, PostId);
                using var connection = _context.Database.GetDbConnection();
                await connection.OpenIfNeeded();
                var users = (await connection.QueryAsync<PhpbbUsers>("SELECT * FROM phpbb_users WHERE user_id IN @userIds", new { userIds = previousPosts.Select(pp => pp.PosterId) })).ToList();
                return (previousPosts, users);
            }
            return (new List<PhpbbPosts>(), new List<PhpbbUsers>());
        }

        private async Task Init(PostingActions action, bool CanCreatePoll, string Header, PhpbbForums curForum)
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
                userMap.Select(map => KeyValuePair.Create(map.Key, $"[url=\"./User?UserId={map.Value}\"]{map.Key}[/url]"))
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
            await _cacheService.SetInCache(await GetActualCacheKey("Action", true), action);
            await _cacheService.SetInCache(await GetActualCacheKey("Header", true), Header);
            await _cacheService.SetInCache(await GetActualCacheKey("ForumName", true), curForum?.ForumName);
            await _cacheService.SetInCache(await GetActualCacheKey("ForumId", true), curForum?.ForumId ?? 0);
            await _cacheService.SetInCache(await GetActualCacheKey("CanCreatePoll", true), CanCreatePoll);
            await _cacheService.SetInCache(await GetActualCacheKey("ForumRulesLink", true), curForum?.ForumRulesLink);
            await _cacheService.SetInCache(await GetActualCacheKey("ForumRules", true), curForum?.ForumRules);
            await _cacheService.SetInCache(await GetActualCacheKey("ForumRulesUid", true), curForum?.ForumRulesUid);
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
            void processAttachments(List<PhpbbAttachments> attachments, int postId)
            {
                for (var i = 0; i < attachments.Count; i++)
                {
                    attachments[i].PostMsgId = postId;
                    attachments[i].TopicId = TopicId.Value;
                    attachments[i].AttachComment = _writingService.PrepareTextForSaving(FileComment[i] ?? string.Empty);
                    attachments[i].IsOrphan = 0;
                }
            }

            async Task insertAttachments(IEnumerable<PhpbbAttachments> attachments)
            {
                using var connection = _context.Database.GetDbConnection();
                await connection.OpenIfNeeded();
                var inserted = await connection.ExecuteAsync(
                    @"INSERT INTO phpbb_attachments (post_msg_id, topic_id, poster_id, is_orphan, physical_filename, real_filename, attach_comment, extension, mimetype, filesize, filetime)
                           VALUES (@PostMsgId, @TopicId, @PosterId, @IsOrphan, @PhysicalFilename, @RealFilename, @AttachComment, @Extension, @Mimetype, @Filesize, @Filetime);",
                    attachments.Select(x => new { x.PostMsgId, x.TopicId, x.PosterId, x.IsOrphan, x.PhysicalFilename, x.RealFilename, x.AttachComment, x.Extension, x.Mimetype, x.Filesize, x.Filetime }) 
                );
                if (inserted != attachments.Count())
                {
                    throw new InvalidOperationException("Not all attachments were inserted.");
                }
            }

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

            var canCreatePoll = await _cacheService.GetFromCache<bool>(await GetActualCacheKey("CanCreatePoll", true));
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

            await _cacheService.RemoveFromCache(await GetActualCacheKey("CanCreatePoll", true));

            PhpbbTopics curTopic = null;
            var attachList = await _cacheService.GetAndRemoveFromCache<List<PhpbbAttachments>>(await GetActualCacheKey("PostAttachments", true)) ?? new List<PhpbbAttachments>();
            var isNewTopic = await _cacheService.GetAndRemoveFromCache<PostingActions>(await GetActualCacheKey("Action", true)) == PostingActions.NewTopic;
            if (isNewTopic)
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
                    PostAttachment = (byte)(attachList.Any() ? 1 : 0),
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
                processAttachments(attachList, post.PostId);
                
                await _postService.CascadePostAdd(_context, post, false);
                await insertAttachments(attachList);
            }
            else
            {
                post.PostText = _writingService.PrepareTextForSaving(newPostText);
                post.BbcodeUid = uid;
                post.BbcodeBitfield = bitfield;
                processAttachments(attachList, post.PostId);

                await insertAttachments(attachList.Where(a => a.AttachId == 0));
                _context.PhpbbAttachments.UpdateRange(attachList.Where(a => a.AttachId != 0));

                post.PostAttachment = (byte)(attachList.Any(a => a.AttachId == 0) && post.PostAttachment == 0 ? 1 : 0);
                await _context.SaveChangesAsync();
                await _postService.CascadePostEdit(_context, post);
            }
            await _context.SaveChangesAsync();

            if (canCreatePoll && !string.IsNullOrWhiteSpace(PollOptions))
            {
                byte pollOptionId = 1;
                ulong id = await _context.PhpbbPollOptions.AsNoTracking().MaxAsync(o => o.Id);

                var options = await _context.PhpbbPollOptions.Where(o => o.TopicId == TopicId).ToListAsync();
                if (pollOptionsArray.Intersect(options.Select(x => x.PollOptionText.Trim()), StringComparer.InvariantCultureIgnoreCase).Count() != options.Count)
                {
                    _context.PhpbbPollOptions.RemoveRange(options);
                    _context.PhpbbPollVotes.RemoveRange(await _context.PhpbbPollVotes.Where(v => v.TopicId == TopicId).ToListAsync());
                    await _context.SaveChangesAsync();
                }

                foreach (var option in pollOptionsArray)
                {
                    var result = await _context.PhpbbPollOptions.AddAsync(new PhpbbPollOptions
                    {
                        PollOptionId = pollOptionId++,
                        PollOptionText = HttpUtility.HtmlEncode(option),
                        PollOptionTotal = 0,
                        TopicId = TopicId.Value,
                        Id = ++id
                    });
                }

                if (curTopic == null)
                {
                    curTopic = await _context.PhpbbTopics.FirstOrDefaultAsync(t => t.TopicId == post.TopicId);
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

        #endregion Helpers
    }
}