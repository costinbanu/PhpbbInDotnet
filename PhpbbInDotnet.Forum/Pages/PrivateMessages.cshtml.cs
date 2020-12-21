using Dapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using PhpbbInDotnet.Objects;
using PhpbbInDotnet.Database;
using PhpbbInDotnet.Services;
using PhpbbInDotnet.Utilities;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using PhpbbInDotnet.Database.Entities;
using System;
using PhpbbInDotnet.Languages;

namespace PhpbbInDotnet.Forum.Pages
{
    public class PrivateMessagesModel : AuthenticatedPageModel
    {
        [BindProperty(SupportsGet = true)]
        public PrivateMessagesPages? Show { get; set; } = PrivateMessagesPages.Inbox;

        [BindProperty(SupportsGet = true)]
        public int? InboxPage { get; set; } = 1;

        [BindProperty(SupportsGet = true)]
        public int? SentPage { get; set; } = 1;

        [BindProperty(SupportsGet = true)]
        public int? MessageId { get; set; }

        [BindProperty]
        public int[] SelectedMessages { get; set; }

        [BindProperty(SupportsGet = true)]
        public PrivateMessagesPages? Source { get; set; }

        public List<PrivateMessageDto> InboxMessages { get; private set; }

        public List<PrivateMessageDto> SentMessages { get; private set; }

        public PrivateMessageDto SelectedMessage { get; private set; }

        public bool SelectedMessageIsMine { get; private set; }

        public bool SelectedMessageIsUnread { get; private set; }

        public Paginator InboxPaginator { get; private set; }

        public Paginator SentPaginator { get; private set; }

        private readonly BBCodeRenderingService _renderingService;

        public PrivateMessagesModel(ForumDbContext context, ForumTreeService forumService, UserService userService, CacheService cacheService,
            BBCodeRenderingService renderingService, IConfiguration config, AnonymousSessionCounter sessionCounter, CommonUtils utils, LanguageProvider languageProvider)
            : base(context, forumService, userService, cacheService, config, sessionCounter, utils, languageProvider)
        {
            _renderingService = renderingService;
        }

        public async Task<IActionResult> OnGet()
            => await WithUserHavingPM(async (user) =>
            {
                using var connection = Context.Database.GetDbConnection();

                if (Show != PrivateMessagesPages.Message)
                {
                    using var multi = await connection.QueryMultipleAsync(
                        @"WITH other AS (
	                        SELECT t.msg_id, 
		                           CASE WHEN t.user_id = @userId THEN t.author_id
			                           ELSE t.user_id
			                           END AS others_id, 
                                   COALESCE(u.username, 'Anonymous') AS others_name, 
                                   u.user_colour AS others_color
                              FROM phpbb_privmsgs_to t
                              LEFT JOIN phpbb_users u ON ((t.user_id = @userId AND t.author_id = u.user_id) OR (t.user_id <> @userId AND t.user_id = u.user_id))
	                         WHERE t.user_id <> t.author_id AND (t.user_id <> @userId OR t.author_id <> @userId)
                        )
                        SELECT m.msg_id AS message_id, 
	                           t.others_id,
                               t.others_name,
                               t.others_color,
                               m.message_subject AS subject,
                               m.message_time,
                               tt.pm_unread
                          FROM phpbb_privmsgs m
                          JOIN other t ON m.msg_id = t.msg_id
                          JOIN phpbb_privmsgs_to tt ON m.msg_id = tt.msg_id
                         WHERE tt.user_id = @userId 
                           AND ((@isInbox AND tt.folder_id >= 0) OR (NOT @isInbox AND tt.folder_id = -1))
                           AND tt.folder_id <> -10
                         ORDER BY message_time DESC
                         LIMIT @skip, @take;
 
                         SELECT COUNT(*) AS cnt
                           FROM phpbb_privmsgs m
                           JOIN phpbb_privmsgs_to t ON m.msg_id = t.msg_id AND t.user_id <> t.author_id AND (t.user_id <> @userId OR t.author_id <> @userId)
                           JOIN  phpbb_privmsgs_to tt ON m.msg_id = tt.msg_id
                          WHERE tt.user_id = @userId
                            AND tt.folder_id <> -10
                            AND ((@isInbox AND tt.folder_id >= 0) OR (NOT @isInbox AND tt.folder_id = -1))",
                        new
                        {
                            user.UserId,
                            isInbox = Show == PrivateMessagesPages.Inbox,
                            skip = Show switch
                            {
                                PrivateMessagesPages.Inbox => ((InboxPage ?? 1) - 1) * Constants.DEFAULT_PAGE_SIZE,
                                PrivateMessagesPages.Sent => ((SentPage ?? 1) - 1) * Constants.DEFAULT_PAGE_SIZE,
                                _ => 0
                            },
                            take = Constants.DEFAULT_PAGE_SIZE
                        }
                    );

                    if (Show == PrivateMessagesPages.Inbox)
                    {
                        InboxMessages = (await multi.ReadAsync<PrivateMessageDto>()).AsList();
                        InboxPaginator = new Paginator(
                            count: unchecked((int)await multi.ReadSingleAsync<long>()),
                            pageNum: InboxPage ?? 1,
                            topicId: null,
                            link: "/PrivateMessages?show=Inbox",
                            pageNumKey: nameof(InboxPage)
                        );
                    }
                    else if (Show == PrivateMessagesPages.Sent)
                    {
                        SentMessages = (await multi.ReadAsync<PrivateMessageDto>()).AsList();
                        SentPaginator = new Paginator(
                            count: unchecked((int)await multi.ReadSingleAsync<long>()),
                            pageNum: SentPage ?? 1,
                            topicId: null,
                            link: "/PrivateMessages?show=Sent",
                            pageNumKey: nameof(SentPage)
                        );
                    }
                }
                else if (MessageId.HasValue && Source.HasValue)
                {
                    SelectedMessageIsMine = Source == PrivateMessagesPages.Sent;
                    using var multi = await connection.QueryMultipleAsync(
                        "SELECT * FROM phpbb_privmsgs WHERE msg_id = @messageId; " +
                        "SELECT * FROM phpbb_privmsgs_to WHERE msg_id = @messageId AND folder_id >= 0; " +
                        "SELECT user_id, author_id FROM phpbb_privmsgs_to WHERE msg_id = @messageId AND user_id <> author_id",
                        new { MessageId, isInbox = Source == PrivateMessagesPages.Inbox }
                    );
                    var message = await multi.ReadFirstOrDefaultAsync<PhpbbPrivmsgs>();
                    var inboxEntry = await multi.ReadFirstOrDefaultAsync<PhpbbPrivmsgsTo>();
                    var userIds = await multi.ReadFirstOrDefaultAsync();
                    SelectedMessageIsUnread = inboxEntry?.PmUnread.ToBool() ?? false;
                    var otherUser = await connection.QueryFirstOrDefaultAsync<PhpbbUsers>("SELECT * FROM phpbb_users WHERE user_id = @userId", 
                        new { userId = userIds.author_id != user.UserId ? userIds.author_id : userIds.user_id}
                    );
                    SelectedMessage = new PrivateMessageDto
                    {
                        MessageId = message.MsgId,
                        OthersId = otherUser.UserId,
                        OthersName = otherUser?.Username ?? "Anonymous",
                        OthersColor = otherUser?.UserColour,
                        OtherHasAvatar = !string.IsNullOrWhiteSpace(otherUser.UserAvatar),
                        Subject = HttpUtility.HtmlDecode(message.MessageSubject),
                        Text = _renderingService.BbCodeToHtml(message.MessageText, message.BbcodeUid),
                        MessageTime = message.MessageTime
                    };

                    if (SelectedMessageIsUnread && !SelectedMessageIsMine)
                    {
                        await connection.ExecuteAsync("UPDATE phpbb_privmsgs_to SET pm_unread = 0 WHERE id = @id", new { id = inboxEntry?.Id ?? 0 });
                    }
                }
                return Page();
            });

        public async Task<IActionResult> OnPostDeleteMessage()
            => await WithUserHavingPM(async (user) =>
            {
                var (Message, IsSuccess) = await UserService.DeletePrivateMessage(MessageId.Value);

                if (IsSuccess ?? false)
                {
                    MessageId = null;
                    return await OnGet();
                }
                else
                {
                    ModelState.AddModelError(nameof(MessageId), Message);
                    Show = PrivateMessagesPages.Message;
                    return await OnGet();
                }
            });

        public async Task<IActionResult> OnPostHideMessage()
            => await WithUserHavingPM(async (user) =>
            {
                var (Message, IsSuccess) = await UserService.HidePrivateMessages(user.UserId, MessageId ?? 0);

                if (IsSuccess ?? false)
                {
                    MessageId = null;
                    return await OnGet();
                }
                else
                {
                    ModelState.AddModelError(nameof(MessageId), Message);
                    Show = PrivateMessagesPages.Message;
                    return await OnGet();
                }
            });

        public async Task<IActionResult> OnPostMarkAsRead()
            => await WithUserHavingPM(async (user) =>
            {
                var connection = Context.Database.GetDbConnection();
                await connection.ExecuteAsync("UPDATE phpbb_privmsgs_to SET pm_unread = 0 WHERE msg_id IN @ids AND author_id <> user_id", new { ids = SelectedMessages?.DefaultIfEmpty() ?? new[] { 0 } });

                return await OnGet();
            });

        public async Task OnPostHideSelectedMessages()
            => await WithUserHavingPM(async (user) =>
            {
                var (Message, IsSuccess) = await UserService.HidePrivateMessages(user.UserId, SelectedMessages);

                if (!(IsSuccess ?? false))
                {
                    ModelState.AddModelError(nameof(MessageId), Message);
                }
                return await OnGet();
            });

        private async Task<IActionResult> WithUserHavingPM(Func<LoggedUser, Task<IActionResult>> toDo)
            => await WithRegisteredUser(async (user) =>
            {
                if (!UserService.HasPrivateMessagePermissions(user))
                {
                    return RedirectToPage("Error", new { isUnauthorised = true });
                }

                return await toDo(user);
            });
    }
}