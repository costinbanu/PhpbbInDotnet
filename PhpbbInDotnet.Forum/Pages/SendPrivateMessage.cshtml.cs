using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using PhpbbInDotnet.Database;
using PhpbbInDotnet.Database.Entities;
using PhpbbInDotnet.Domain;
using PhpbbInDotnet.Domain.Extensions;
using PhpbbInDotnet.Domain.Utilities;
using PhpbbInDotnet.Forum.Models;
using PhpbbInDotnet.Languages;
using PhpbbInDotnet.Objects;
using PhpbbInDotnet.Services;
using System;
using System.Threading.Tasks;
using System.Web;

namespace PhpbbInDotnet.Forum.Pages
{
    [ValidateAntiForgeryToken]
    public class SendPrivateMessageModel : BasePostingModel
    {
        private readonly IWritingToolsService _writingService;
        private readonly IConfiguration _config;

        public SendPrivateMessageModel(IWritingToolsService writingService, IConfiguration config, IForumTreeService forumService, 
            IUserService userService, ISqlExecuter sqlExecuter, ITranslationProvider translationProvider, IConfiguration configuration)
            : base(forumService, userService, sqlExecuter, translationProvider, configuration)
        {
            _writingService = writingService;
            _config = config;
        }

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


        public Task<IActionResult> OnGet()
            => WithRegisteredUser(async (usr) =>
            {
                if ((PostId ?? 0) > 0)
                {
                    var post = await SqlExecuter.QueryFirstOrDefaultAsync<PhpbbPosts>("SELECT * FROM phpbb_posts WHERE post_id = @postId", new { PostId });
                    if (post != null)
                    {
                        var author = await SqlExecuter.QueryFirstOrDefaultAsync<PhpbbUsers>("SELECT * FROM phpbb_users WHERE user_id = @posterId", new { post.PosterId });
                        if ((author?.UserId ?? Constants.ANONYMOUS_USER_ID) != Constants.ANONYMOUS_USER_ID)
                        {
                            PostTitle = HttpUtility.HtmlDecode(post.PostSubject);
                            PostText = $"[quote]\n{_writingService.CleanBbTextForDisplay(post.PostText, post.BbcodeUid)}\n[/quote]\n[url={_config.GetValue<string>("BaseUrl").Trim('/')}/ViewTopic?postId={PostId}&handler=byPostId]{PostTitle}[/url]\n";
                            ReceiverId = author!.UserId;
                            ReceiverName = author.Username;
                        }
                        else
                        {
                            return RedirectToPage("Error", new { CustomErrorMessage = await CompressionUtility.CompressAndEncode(TranslationProvider.Errors[Language, "RECEIVER_DOESNT_EXIST"]) });
                        }
                    }
                    else
                    {
                        return RedirectToPage("Error", new { CustomErrorMessage = await CompressionUtility.CompressAndEncode(TranslationProvider.Errors[Language, "POST_DOESNT_EXIST"]) });
                    }
                }
                else if ((PrivateMessageId ?? 0) > 0 && (ReceiverId ?? Constants.ANONYMOUS_USER_ID) != Constants.ANONYMOUS_USER_ID)
                {
                    var msg = await SqlExecuter.QueryFirstOrDefaultAsync<PhpbbPrivmsgs>("SELECT * FROM phpbb_privmsgs WHERE msg_id = @privateMessageId", new { PrivateMessageId });
                    if (msg != null && ReceiverId == msg.AuthorId)
                    {
                        var author = await SqlExecuter.QueryFirstOrDefaultAsync<PhpbbUsers>("SELECT * FROM phpbb_users WHERE user_id = @receiverId", new { ReceiverId });
                        if ((author?.UserId ?? Constants.ANONYMOUS_USER_ID) != Constants.ANONYMOUS_USER_ID)
                        {
                            var title = HttpUtility.HtmlDecode(msg.MessageSubject);
                            PostTitle = title.StartsWith(Constants.REPLY) ? title : $"{Constants.REPLY}{title}";
                            PostText = $"[quote]\n{_writingService.CleanBbTextForDisplay(msg.MessageText, msg.BbcodeUid)}\n[/quote]\n";
                            ReceiverName = author!.Username;
                        }
                        else
                        {
                            return RedirectToPage("Error", new { CustomErrorMessage = await CompressionUtility.CompressAndEncode(TranslationProvider.Errors[Language, "RECEIVER_DOESNT_EXIST"]) });
                        }
                    }
                    else
                    {
                        return RedirectToPage("Error", new { CustomErrorMessage = await CompressionUtility.CompressAndEncode(TranslationProvider.Errors[Language, "PM_DOESNT_EXIST"]) });
                    }
                }
                else if ((ReceiverId ?? Constants.ANONYMOUS_USER_ID) != Constants.ANONYMOUS_USER_ID)
                {
                    ReceiverName = (await SqlExecuter.QueryFirstOrDefaultAsync<PhpbbUsers>("SELECT * FROM phpbb_users WHERE user_id = @receiverId", new { ReceiverId }))?.Username;
                }

                Action = PostingActions.NewPrivateMessage;

                return Page();
            });

        public async Task<IActionResult> OnGetEdit()
            => await WithRegisteredUser(async (user) =>
            {
                ThrowIfEntireForumIsReadOnly();

                var pm = await SqlExecuter.QueryFirstOrDefaultAsync<PhpbbPrivmsgs>(
                    "SELECT * FROM phpbb_privmsgs WHERE msg_id = @privateMessageId", 
                    new { PrivateMessageId });
                PostText = _writingService.CleanBbTextForDisplay(pm.MessageText, pm.BbcodeUid);
                PostTitle = HttpUtility.HtmlDecode(pm.MessageSubject);
                if ((ReceiverId ?? Constants.ANONYMOUS_USER_ID) != Constants.ANONYMOUS_USER_ID)
                {
                    ReceiverName = (await SqlExecuter.QueryFirstOrDefaultAsync<PhpbbUsers>(
                        "SELECT * FROM phpbb_users WHERE user_id = @receiverId", 
                        new { ReceiverId }))?.Username;
                }
                Action = PostingActions.EditPrivateMessage;

                return Page();
            });

        public Task<IActionResult> OnPostSubmit()
            => WithRegisteredUser(user => WithValidInput(async () =>
            {
                var lang = Language;

                if ((ReceiverId ?? Constants.ANONYMOUS_USER_ID) == Constants.ANONYMOUS_USER_ID)
                {
                    return PageWithError(nameof(ReceiverName), TranslationProvider.Errors[lang, "ENTER_VALID_RECEIVER"]);
                }

                var (Message, IsSuccess) = Action switch
                {
                    PostingActions.NewPrivateMessage => await UserService.SendPrivateMessage(user, ReceiverId!.Value, HttpUtility.HtmlEncode(PostTitle)!, await _writingService.PrepareTextForSaving(PostText), PageContext, HttpContext),
                    PostingActions.EditPrivateMessage => await UserService.EditPrivateMessage(PrivateMessageId!.Value, HttpUtility.HtmlEncode(PostTitle)!, await _writingService.PrepareTextForSaving(PostText)),
                    _ => ("Unknown action", false)
                };

                return IsSuccess switch
                {
                    true => RedirectToPage("PrivateMessages", new { show = PrivateMessagesPages.Sent }),
                    _ => PageWithError(nameof(PostText), Message)
                };
            }));

        public Task<IActionResult> OnPostPreview()
            => WithRegisteredUser(user => WithValidInput(async () =>
            {
                var lang = Language;
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
            }));
	}
}
