using LazyCache;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using PhpbbInDotnet.Database;
using PhpbbInDotnet.Database.Entities;
using PhpbbInDotnet.Languages;
using PhpbbInDotnet.Objects;
using PhpbbInDotnet.Services;
using PhpbbInDotnet.Utilities;
using PhpbbInDotnet.Utilities.Extensions;
using System;
using System.Threading.Tasks;
using System.Web;

namespace PhpbbInDotnet.Forum.Pages
{
    [ValidateAntiForgeryToken]
    public class SendPrivateMessageModel : AuthenticatedPageModel
    {
        private readonly IWritingToolsService _writingService;
        private readonly IConfiguration _config;

        [BindProperty]
        public string? PostTitle { get; set; }

        [BindProperty]
        public string? PostText { get; set; }

        [BindProperty(SupportsGet = true)]
        public int? PostId { get; set; }

        [BindProperty(SupportsGet = true)]
        public int? ReceiverId { get; set; }

        [BindProperty]
        public string? ReceiverName { get; set; }

        [BindProperty(SupportsGet = true)]
        public int? PrivateMessageId { get; set; }

        [BindProperty(SupportsGet = true)]
        public PostingActions Action { get; set; } = PostingActions.NewPrivateMessage;

        public PostDto? PreviewablePost { get; set; }

        public SendPrivateMessageModel(ICommonUtils utils, IForumDbContext context, IForumTreeService forumService, IUserService userService, IAppCache cacheService, 
            LanguageProvider languageProvider, IWritingToolsService writingService, IConfiguration config)
            : base(context, forumService, userService, cacheService, utils, languageProvider)
        {
            _writingService = writingService;
            _config = config;
        }

        public Task<IActionResult> OnGet()
            => WithRegisteredUser(async (usr) =>
            {
                var lang = GetLanguage();
                var sqlExec = Context.GetSqlExecuter();

                if ((PostId ?? 0) > 0)
                {
                    var post = await sqlExec.QueryFirstOrDefaultAsync<PhpbbPosts>("SELECT * FROM phpbb_posts WHERE post_id = @postId", new { PostId });
                    if (post != null)
                    {
                        var author = await sqlExec.QueryFirstOrDefaultAsync<PhpbbUsers>("SELECT * FROM phpbb_users WHERE user_id = @posterId", new { post.PosterId });
                        if ((author?.UserId ?? Constants.ANONYMOUS_USER_ID) != Constants.ANONYMOUS_USER_ID)
                        {
                            PostTitle = HttpUtility.HtmlDecode(post.PostSubject);
                            PostText = $"[quote]\n{_writingService.CleanBbTextForDisplay(post.PostText, post.BbcodeUid)}\n[/quote]\n[url={_config.GetValue<string>("BaseUrl").Trim('/')}/ViewTopic?postId={PostId}&handler=byPostId]{PostTitle}[/url]\n";
                            ReceiverId = author!.UserId;
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
                    var msg = await sqlExec.QueryFirstOrDefaultAsync<PhpbbPrivmsgs>("SELECT * FROM phpbb_privmsgs WHERE msg_id = @privateMessageId", new { PrivateMessageId });
                    if (msg != null && ReceiverId == msg.AuthorId)
                    {
                        var author = await sqlExec.QueryFirstOrDefaultAsync<PhpbbUsers>("SELECT * FROM phpbb_users WHERE user_id = @receiverId", new { ReceiverId });
                        if ((author?.UserId ?? Constants.ANONYMOUS_USER_ID) != Constants.ANONYMOUS_USER_ID)
                        {
                            var title = HttpUtility.HtmlDecode(msg.MessageSubject);
                            PostTitle = title.StartsWith(Constants.REPLY) ? title : $"{Constants.REPLY}{title}";
                            PostText = $"[quote]\n{_writingService.CleanBbTextForDisplay(msg.MessageText, msg.BbcodeUid)}\n[/quote]\n";
                            ReceiverName = author!.Username;
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
                    ReceiverName = (await sqlExec.QueryFirstOrDefaultAsync<PhpbbUsers>("SELECT * FROM phpbb_users WHERE user_id = @receiverId", new { ReceiverId }))?.Username;
                }

                Action = PostingActions.NewPrivateMessage;

                return Page();
            });

        public async Task<IActionResult> OnGetEdit()
            => await WithRegisteredUser(async (user) =>
            {
                var sqlExec = Context.GetSqlExecuter();

                var pm = await sqlExec.QueryFirstOrDefaultAsync<PhpbbPrivmsgs>("SELECT * FROM phpbb_privmsgs WHERE msg_id = @privateMessageId", new { PrivateMessageId });
                PostText = _writingService.CleanBbTextForDisplay(pm.MessageText, pm.BbcodeUid);
                PostTitle = HttpUtility.HtmlDecode(pm.MessageSubject);
                if ((ReceiverId ?? Constants.ANONYMOUS_USER_ID) != Constants.ANONYMOUS_USER_ID)
                {
                    ReceiverName = (await sqlExec.QueryFirstOrDefaultAsync<PhpbbUsers>("SELECT * FROM phpbb_users WHERE user_id = @receiverId", new { ReceiverId }))?.Username;
                }
                Action = PostingActions.EditPrivateMessage;

                return Page();
            });

        public Task<IActionResult> OnPostSubmit()
            => WithRegisteredUser(async (user) =>
            {
                var lang = GetLanguage();

                if ((ReceiverId ?? 1) == 1)
                {
                    return PageWithError(nameof(ReceiverName), LanguageProvider.Errors[lang, "ENTER_VALID_RECEIVER"]);
                }

                if ((PostTitle?.Trim()?.Length ?? 0) < 3)
                {
                    return PageWithError(nameof(PostTitle), LanguageProvider.Errors[lang, "TITLE_TOO_SHORT"]);
                }

                if ((PostTitle?.Length ?? 0) > 255)
                {
                    return PageWithError(nameof(PostTitle), LanguageProvider.Errors[lang, "TITLE_TOO_LONG"]);
                }

                if ((PostText?.Trim()?.Length ?? 0) < 3)
                {
                    return PageWithError(nameof(PostText), LanguageProvider.Errors[lang, "POST_TOO_SHORT"]);
                }

                var (Message, IsSuccess) = Action switch
                {
                    PostingActions.NewPrivateMessage => await UserService.SendPrivateMessage(user.UserId, user.Username!, ReceiverId!.Value, HttpUtility.HtmlEncode(PostTitle)!, await _writingService.PrepareTextForSaving(PostText), PageContext, HttpContext),
                    PostingActions.EditPrivateMessage => await UserService.EditPrivateMessage(PrivateMessageId!.Value, HttpUtility.HtmlEncode(PostTitle)!, await _writingService.PrepareTextForSaving(PostText)),
                    _ => ("Unknown action", false)
                };

                return IsSuccess switch
                {
                    true => RedirectToPage("PrivateMessages", new { show = PrivateMessagesPages.Sent }),
                    _ => PageWithError(nameof(PostText), Message)
                };
            });

        public Task<IActionResult> OnPostPreview()
            => WithRegisteredUser(async user =>
            {
                var lang = GetLanguage();
                if ((PostTitle?.Trim()?.Length ?? 0) < 3)
                {
                    return PageWithError(nameof(PostTitle), LanguageProvider.Errors[lang, "TITLE_TOO_SHORT"]);
                }

                if ((PostText?.Trim()?.Length ?? 0) < 3)
                {
                    return PageWithError(nameof(PostText), LanguageProvider.Errors[lang, "POST_TOO_SHORT"]);
                }

                var sqlExec = Context.GetSqlExecuter();

                var newPostText = PostText;
                newPostText = HttpUtility.HtmlEncode(newPostText);

                PreviewablePost = new PostDto
                {
                    AuthorColor = user.UserColor,
                    AuthorId = user.UserId,
                    AuthorName = user.Username,
                    PostSubject = HttpUtility.HtmlEncode(PostTitle),
                    PostText = await _writingService.PrepareTextForSaving(newPostText),
                    PostTime = DateTime.UtcNow.ToUnixTimestamp()
                };
                return Page();
            });

        private IActionResult PageWithError(string errorKey, string errorMessage)
        {
            ModelState.AddModelError(errorKey, errorMessage);
            return Page();
        }
    }
}
