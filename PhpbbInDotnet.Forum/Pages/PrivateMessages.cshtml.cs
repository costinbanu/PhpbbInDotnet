using Dapper;
using LazyCache;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PhpbbInDotnet.Database;
using PhpbbInDotnet.Languages;
using PhpbbInDotnet.Objects;
using PhpbbInDotnet.Services;
using PhpbbInDotnet.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
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

        public PrivateMessagesModel(IForumDbContext context, IForumTreeService forumService, IUserService userService, IAppCache cache,
            IBBCodeRenderingService renderingService, ICommonUtils utils, LanguageProvider languageProvider)
            : base(context, forumService, userService, cache, utils, languageProvider)
        {
            _renderingService = renderingService;
        }

        public async Task<IActionResult> OnGet()
            => await WithUserHavingPM(async (user) =>
            {
                var sqlExecuter = Context.GetSqlExecuter();
                if (Show != PrivateMessagesPages.Message)
                {
                    var pageNum = Show switch
                    {
                        PrivateMessagesPages.Inbox => Paginator.NormalizePageNumberLowerBound(InboxPage),
                        PrivateMessagesPages.Sent => Paginator.NormalizePageNumberLowerBound(SentPage),
                        _ => 1
                    };
                    await Utils.RetryOnceAsync(
                        toDo: async () =>
                        {
                            var messageTask = sqlExecuter.QueryAsync<PrivateMessageDto>(
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
                                 LIMIT @skip, @take",
                                new
                                {
                                    user.UserId,
                                    isInbox = Show == PrivateMessagesPages.Inbox,
                                    skip = (pageNum - 1) * Constants.DEFAULT_PAGE_SIZE,
                                    take = Constants.DEFAULT_PAGE_SIZE
                                });

                            var countTask = sqlExecuter.ExecuteScalarAsync<int>(
                                @"SELECT COUNT(*) AS cnt
                                    FROM phpbb_privmsgs m
                                    JOIN phpbb_privmsgs_to t ON m.msg_id = t.msg_id AND t.user_id <> t.author_id AND (t.user_id <> @userId OR t.author_id <> @userId)
                                    JOIN  phpbb_privmsgs_to tt ON m.msg_id = tt.msg_id
                                   WHERE tt.user_id = @userId
                                     AND tt.folder_id <> -10
                                     AND ((@isInbox AND tt.folder_id >= 0) OR (NOT @isInbox AND tt.folder_id = -1))",
                                new
                                {
                                    user.UserId,
                                    isInbox = Show == PrivateMessagesPages.Inbox
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
                    var messagesTask = (
                        from pm in Context.PhpbbPrivmsgs.AsNoTracking()
                        where pm.MsgId == MessageId
                        select pm).FirstAsync();
                    var msgToTask = (
                        from mt in Context.PhpbbPrivmsgsTo.AsNoTracking()
                        where mt.MsgId == MessageId && mt.FolderId >= 0
                        select mt).FirstOrDefaultAsync();
                    var otherUserTask = (
                        from mt in Context.PhpbbPrivmsgsTo.AsNoTracking()
                        where mt.MsgId == MessageId && mt.UserId != mt.AuthorId
                        let userId = mt.AuthorId != user.UserId ? mt.AuthorId : mt.UserId
                        join u in Context.PhpbbUsers.AsNoTracking()
                        on userId equals u.UserId
                        select u).FirstAsync();
                   
                    await Task.WhenAll(messagesTask, msgToTask, otherUserTask);

                    var message = await messagesTask;
                    var inboxEntry = await msgToTask;
                    SelectedMessageIsUnread = inboxEntry?.PmUnread.ToBool() ?? false;
                    var otherUser = await otherUserTask; 
                    SelectedMessage = new PrivateMessageDto
                    {
                        MessageId = message.MsgId,
                        OthersId = otherUser.UserId,
                        OthersName = otherUser?.Username ?? Constants.ANONYMOUS_USER_NAME,
                        OthersColor = otherUser?.UserColour,
                        OthersAvatar = otherUser?.UserAvatar,
                        Subject = HttpUtility.HtmlDecode(message.MessageSubject),
                        Text = _renderingService.BbCodeToHtml(message.MessageText, message.BbcodeUid),
                        MessageTime = message.MessageTime
                    };

                    if (SelectedMessageIsUnread && !SelectedMessageIsMine)
                    {
                        await sqlExecuter.ExecuteAsync("UPDATE phpbb_privmsgs_to SET pm_unread = 0 WHERE id = @id", new { id = inboxEntry?.Id ?? 0 });
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
                var sqlExecuter = Context.GetSqlExecuter();
                await sqlExecuter.ExecuteAsync(
                    "UPDATE phpbb_privmsgs_to SET pm_unread = 0 WHERE msg_id IN @ids AND author_id <> user_id", 
                    new { ids = SelectedMessages?.DefaultIfEmpty() ?? new[] { 0 } });

                return await OnGet();
            });

        public async Task OnPostHideSelectedMessages()
            => await WithUserHavingPM(async (user) =>
            {
                var (Message, IsSuccess) = await UserService.HidePrivateMessages(user.UserId, SelectedMessages!);

                if (!(IsSuccess ?? false))
                {
                    ModelState.AddModelError(nameof(MessageId), Message);
                }
                return await OnGet();
            });

        private async Task<IActionResult> WithUserHavingPM(Func<AuthenticatedUserExpanded, Task<IActionResult>> toDo)
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