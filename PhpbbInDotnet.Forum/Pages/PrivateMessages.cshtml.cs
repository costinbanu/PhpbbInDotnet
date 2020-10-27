using Dapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using PhpbbInDotnet.DTOs;
using PhpbbInDotnet.Database;
using PhpbbInDotnet.Services;
using PhpbbInDotnet.Utilities;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;

namespace PhpbbInDotnet.Forum.Pages
{
    public class PrivateMessagesModel : ModelWithLoggedUser
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

        public List<PrivateMessageDto> InboxMessages { get; private set; }

        public List<PrivateMessageDto> SentMessages { get; private set; }

        public PrivateMessageDto SelectedMessage { get; private set; }

        public bool SelectedMessageIsMine { get; private set; }

        public bool SelectedMessageIsUnread { get; private set; }
        
        public Paginator InboxPaginator { get; private set; }

        public Paginator SentPaginator { get; private set; }

        private readonly BBCodeRenderingService _renderingService;
        
        public PrivateMessagesModel(ForumDbContext context, ForumTreeService forumService, UserService userService, CacheService cacheService, 
            BBCodeRenderingService renderingService, IConfiguration config, AnonymousSessionCounter sessionCounter, CommonUtils utils)
            : base(context, forumService, userService, cacheService, config, sessionCounter, utils)
        {
            _renderingService = renderingService;
        }

        public async Task<IActionResult> OnGet()
            => await WithRegisteredUser(async (user) =>
            {
                if (!_userService.HasPrivateMessagePermissions(user))
                {
                    return BadRequest("Utilizatorul nu are acces la mesageria privată.");
                }

                InboxPaginator = new Paginator(
                    count: await _context.PhpbbPrivmsgsTo.CountAsync(x => x.UserId == user.UserId && x.AuthorId != x.UserId),
                    pageNum: InboxPage ?? 1,
                    topicId: null,
                    link: "/PrivateMessages?show=Inbox",
                    pageNumKey: nameof(InboxPage)
                );

                SentPaginator = new Paginator(
                    count: await _context.PhpbbPrivmsgsTo.CountAsync(x => x.AuthorId == user.UserId && x.AuthorId != x.UserId),
                    pageNum: SentPage ?? 1,
                    topicId: null,
                    link: "/PrivateMessages?show=Sent",
                    pageNumKey: nameof(SentPage)
                );

                InboxMessages = await (
                    from m in _context.PhpbbPrivmsgs.AsNoTracking()

                    join mt in _context.PhpbbPrivmsgsTo.AsNoTracking()
                    on m.MsgId equals mt.MsgId
                    into joined

                    from j in joined
                    where j.UserId == user.UserId && j.AuthorId != j.UserId && j.FolderId != -1

                    join u in _context.PhpbbUsers.AsNoTracking()
                    on j.AuthorId equals u.UserId
                    into joinedUsers

                    from ju in joinedUsers.DefaultIfEmpty()
                    orderby m.MessageTime descending
                    select new PrivateMessageDto
                    {
                        MessageId = m.MsgId,
                        OthersId = j.AuthorId,
                        OthersName = ju == null ? "Anonymous" : ju.Username,
                        OthersColor = ju == null ? null : ju.UserColour,
                        Subject = m.MessageSubject,
                        Time = m.MessageTime.ToUtcTime(),
                        Unread = j.PmUnread
                    }
                ).Skip(((InboxPage ?? 1) - 1) * InboxPaginator.PageSize).Take(InboxPaginator.PageSize).ToListAsync();

                SentMessages = await (
                    from m in _context.PhpbbPrivmsgs.AsNoTracking()

                    join mt in _context.PhpbbPrivmsgsTo.AsNoTracking()
                    on m.MsgId equals mt.MsgId
                    into joined

                    from j in joined
                    where j.AuthorId == user.UserId && j.AuthorId != j.UserId && j.FolderId != -1

                    join u in _context.PhpbbUsers.AsNoTracking()
                    on j.UserId equals u.UserId
                    into joinedUsers

                    from ju in joinedUsers.DefaultIfEmpty()
                    orderby m.MessageTime descending
                    select new PrivateMessageDto
                    {
                        MessageId = m.MsgId,
                        OthersId = j.UserId,
                        OthersName = ju == null ? "Anonymous" : ju.Username,
                        OthersColor = ju == null ? null : ju.UserColour,
                        Subject = m.MessageSubject,
                        Time = m.MessageTime.ToUtcTime(),
                        Unread = 0
                    }
                ).Skip(((SentPage ?? 1) - 1) * SentPaginator.PageSize).Take(SentPaginator.PageSize).ToListAsync();

                if (Show == PrivateMessagesPages.Message && MessageId.HasValue)
                {
                    var msg = await _context.PhpbbPrivmsgs.AsNoTracking().FirstOrDefaultAsync(x => x.MsgId == MessageId);
                    var to = await _context.PhpbbPrivmsgsTo.FirstOrDefaultAsync(x => x.MsgId == MessageId && x.AuthorId != x.UserId);
                    SelectedMessageIsMine = to.AuthorId == user.UserId;
                    SelectedMessageIsUnread = to.PmUnread.ToBool();
                    var other = await _context.PhpbbUsers.AsNoTracking().FirstOrDefaultAsync(x => x.UserId == (SelectedMessageIsMine ? to.UserId : to.AuthorId));
                    SelectedMessage = new PrivateMessageDto
                    {
                        MessageId = msg.MsgId,
                        OthersId = other.UserId,
                        OthersName = other?.Username ?? "Anonymous",
                        OthersColor = other?.UserColour,
                        OtherHasAvatar = !string.IsNullOrWhiteSpace(other.UserAvatar),
                        Subject = HttpUtility.HtmlDecode(msg.MessageSubject),
                        Text = _renderingService.BbCodeToHtml(msg.MessageText, msg.BbcodeUid),
                        Time = msg.MessageTime.ToUtcTime()
                    };

                    if (to.PmUnread.ToBool() && !SelectedMessageIsMine)
                    {
                        to.PmUnread = 0;
                        _context.PhpbbPrivmsgsTo.Update(to);
                        await _context.SaveChangesAsync();
                    }
                }
                return Page();
            });

        public async Task<IActionResult> OnPostDeleteMessage()
            => await WithRegisteredUser(async (user) =>
            {
                if (!_userService.HasPrivateMessagePermissions(user))
                {
                    return BadRequest("Utilizatorul nu are acces la mesageria privată.");
                }

                var (Message, IsSuccess) = await _userService.DeletePrivateMessage(MessageId.Value);

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
            => await WithRegisteredUser(async (user) =>
            {
                if (!_userService.HasPrivateMessagePermissions(user))
                {
                    return BadRequest("Utilizatorul nu are acces la mesageria privată.");
                }

                var to = await _context.PhpbbPrivmsgsTo.FirstOrDefaultAsync(x => x.MsgId == MessageId && x.AuthorId != x.UserId);
                var (Message, IsSuccess) = await _userService.HidePrivateMessage(MessageId ?? 0, to.AuthorId, to.UserId);

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
        {
            var connection = _context.Database.GetDbConnection();
            await connection.OpenIfNeededAsync();
            await connection.ExecuteAsync("UPDATE phpbb_privmsgs_to SET pm_unread = 0 WHERE msg_id IN @ids AND author_id <> user_id", new { ids = SelectedMessages?.DefaultIfEmpty() ?? new[] { 0 } });

            return await OnGet();
        }
    }
}