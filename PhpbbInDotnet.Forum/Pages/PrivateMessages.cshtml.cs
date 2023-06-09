using Dapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using PhpbbInDotnet.Database.Entities;
using PhpbbInDotnet.Database.SqlExecuter;
using PhpbbInDotnet.Domain;
using PhpbbInDotnet.Domain.Extensions;
using PhpbbInDotnet.Domain.Utilities;
using PhpbbInDotnet.Forum.Models;
using PhpbbInDotnet.Languages;
using PhpbbInDotnet.Objects;
using PhpbbInDotnet.Services;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Web;

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
        public int[]? SelectedMessages { get; set; }

        [BindProperty(SupportsGet = true)]
        public PrivateMessagesPages? Source { get; set; }

        public List<PrivateMessageDto>? InboxMessages { get; private set; }

        public List<PrivateMessageDto>? SentMessages { get; private set; }

        public PrivateMessageDto? SelectedMessage { get; private set; }

        public bool SelectedMessageIsMine { get; private set; }

        public bool SelectedMessageIsUnread { get; private set; }

        public Paginator? InboxPaginator { get; private set; }

        public Paginator? SentPaginator { get; private set; }

        private readonly IBBCodeRenderingService _renderingService;

        public PrivateMessagesModel(IForumTreeService forumService, IUserService userService, ISqlExecuter sqlExecuter, 
            ITranslationProvider translationProvider, IBBCodeRenderingService renderingService, IConfiguration configuration)
            : base(forumService, userService, sqlExecuter, translationProvider, configuration)
        {
            _renderingService = renderingService;
        }

        public async Task<IActionResult> OnGet()
            => await WithUserHavingPM(async (user) =>
            {
                if (Show != PrivateMessagesPages.Message)
                {
                    var pageNum = Show switch
                    {
                        PrivateMessagesPages.Inbox => Paginator.NormalizePageNumberLowerBound(InboxPage),
                        PrivateMessagesPages.Sent => Paginator.NormalizePageNumberLowerBound(SentPage),
                        _ => 1
                    };
                    await ResiliencyUtility.RetryOnceAsync(
                        toDo: async () =>
                        {
                            var messageTask = SqlExecuter
                                .WithPagination((pageNum - 1) * Constants.DEFAULT_PAGE_SIZE, Constants.DEFAULT_PAGE_SIZE)
                                .QueryAsync<PrivateMessageDto>(
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
                                       AND ((@isInbox = 1 AND tt.folder_id >= 0) OR (@isInbox = 0 AND tt.folder_id = -1))
                                       AND tt.folder_id <> -10
                                     ORDER BY message_time DESC",
                                    new
                                    {
                                        user.UserId,
                                        isInbox = (Show == PrivateMessagesPages.Inbox).ToByte(),
                                    });

                            var countTask = SqlExecuter.ExecuteScalarAsync<int>(
                                @"SELECT COUNT(*) AS cnt
                                    FROM phpbb_privmsgs m
                                    JOIN phpbb_privmsgs_to t ON m.msg_id = t.msg_id AND t.user_id <> t.author_id AND (t.user_id <> @userId OR t.author_id <> @userId)
                                    JOIN  phpbb_privmsgs_to tt ON m.msg_id = tt.msg_id
                                   WHERE tt.user_id = @userId
                                     AND tt.folder_id <> -10
                                     AND ((@isInbox = 1 AND tt.folder_id >= 0) OR (@isInbox = 0 AND tt.folder_id = -1))",
                                new
                                {
                                    user.UserId,
                                    isInbox = (Show == PrivateMessagesPages.Inbox).ToByte()
                                });

                            await Task.WhenAll(messageTask, countTask);

                            switch (Show)
                            {
                                case PrivateMessagesPages.Inbox:
                                    InboxMessages = (await messageTask).AsList();
                                    InboxPaginator = new Paginator(
                                        count: await countTask,
                                        pageNum: pageNum,
                                        topicId: null,
                                        link: "/PrivateMessages?show=Inbox",
                                        pageNumKey: nameof(InboxPage)
                                    );
                                    break;

                                case PrivateMessagesPages.Sent:
                                    SentMessages = (await messageTask).AsList();
                                    SentPaginator = new Paginator(
                                        count: await countTask,
                                        pageNum: pageNum,
                                        topicId: null,
                                        link: "/PrivateMessages?show=Sent",
                                        pageNumKey: nameof(SentPage)
                                    );
                                    break;
                            }
                        },
                        evaluateSuccess: () => Show switch
                        {
                            PrivateMessagesPages.Inbox => InboxMessages!.Count > 0 && pageNum == InboxPaginator!.CurrentPage,
                            PrivateMessagesPages.Sent => SentMessages!.Count > 0 && pageNum == SentPaginator!.CurrentPage,
                            _ => true
                        },
                        fix: () =>
                        {
                            switch (Show)
                            {
                                case PrivateMessagesPages.Inbox:
                                    pageNum = InboxPaginator!.CurrentPage;
                                    break;

                                case PrivateMessagesPages.Sent:
                                    pageNum = SentPaginator!.CurrentPage;
                                    break;
                            }
                        });
                    
                }
                else if (MessageId.HasValue && Source.HasValue)
                {
                    SelectedMessageIsMine = Source == PrivateMessagesPages.Sent;
                    var messagesTask = SqlExecuter.QuerySingleOrDefaultAsync<PhpbbPrivmsgs>(
                        "SELECT * FROM phpbb_privmsgs WHERE msg_id = @messageId",
                        new { MessageId });
                    
                    var msgToTask = SqlExecuter.QuerySingleOrDefaultAsync<PhpbbPrivmsgsTo>(
                        "SELECT * FROM phpbb_privmsgs_to WHERE msg_id = @messageId AND folder_id >= 0",
                        new { MessageId });

                    var otherUserTask = SqlExecuter.QueryFirstOrDefaultAsync<PhpbbUsers>(
                        @"SELECT u.* 
                            FROM phpbb_privmsgs_to mt
                            JOIN phpbb_users u ON 
                                 (mt.author_id <> @currentUserId AND mt.author_id = u.user_id) OR 
                                 (mt.author_id = @currentUserId AND mt.user_id = u.user_id)
                           WHERE mt.msg_id = @messageId AND mt.user_id <> mt.author_id",
                        new
                        {
                            MessageId,
                            currentUserId = user.UserId
                        });
                   
                    await Task.WhenAll(messagesTask, msgToTask, otherUserTask);

                    var message = await messagesTask;
                    var inboxEntry = await msgToTask;
                    var otherUser = await otherUserTask;

                    if (message is null)
                    {
                        return NotFound();
                    }

                    SelectedMessageIsUnread = inboxEntry?.PmUnread.ToBool() ?? false;
                    SelectedMessage = new PrivateMessageDto
                    {
                        MessageId = message.MsgId,
                        OthersId = otherUser?.UserId ?? Constants.ANONYMOUS_USER_ID,
                        OthersName = otherUser?.Username ?? Constants.ANONYMOUS_USER_NAME,
                        OthersColor = otherUser?.UserColour,
                        OthersAvatar = otherUser?.UserAvatar,
                        Subject = HttpUtility.HtmlDecode(message.MessageSubject),
                        Text = _renderingService.BbCodeToHtml(message.MessageText, message.BbcodeUid),
                        MessageTime = message.MessageTime
                    };

                    if (SelectedMessageIsUnread && !SelectedMessageIsMine)
                    {
                        await SqlExecuter.ExecuteAsync("UPDATE phpbb_privmsgs_to SET pm_unread = 0 WHERE id = @id", new { id = inboxEntry?.Id ?? 0 });
                    }
                }
                return Page();
            });

        public async Task<IActionResult> OnPostDeleteMessage()
            => await WithUserHavingPM(async (user) =>
            {
                var (Message, IsSuccess) = await UserService.DeletePrivateMessage(MessageId!.Value);

                if (IsSuccess ?? false)
                {
                    MessageId = null;
                    return await OnGet();
                }
                else
                {
                    return await PageWithErrorAsync(nameof(MessageId), Message, toDoBeforeReturn: () => Show = PrivateMessagesPages.Message, resultFactory: OnGet);
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
                    return await PageWithErrorAsync(nameof(MessageId), Message, toDoBeforeReturn: () => Show = PrivateMessagesPages.Message, resultFactory: OnGet);
                }
            });

        public async Task<IActionResult> OnPostMarkAsRead()
            => await WithUserHavingPM(async (user) =>
            {
                await SqlExecuter.ExecuteAsync(
                    "UPDATE phpbb_privmsgs_to SET pm_unread = 0 WHERE msg_id IN @ids AND author_id <> user_id", 
                    new { ids = SelectedMessages.DefaultIfNullOrEmpty() });

                return await OnGet();
            });

        public async Task OnPostHideSelectedMessages()
            => await WithUserHavingPM(async (user) =>
            {
                var (Message, IsSuccess) = await UserService.HidePrivateMessages(user.UserId, SelectedMessages!);

                if (!IsSuccess != true)
                {
                    return PageWithError(nameof(MessageId), Message);
                }
                return await OnGet();
            });

        private async Task<IActionResult> WithUserHavingPM(Func<ForumUserExpanded, Task<IActionResult>> toDo)
            => await WithRegisteredUser(async (user) =>
            {
                if (!user.HasPrivateMessagePermissions)
                {
                    return Unauthorized();
                }

                return await toDo(user);
            });
    }
}