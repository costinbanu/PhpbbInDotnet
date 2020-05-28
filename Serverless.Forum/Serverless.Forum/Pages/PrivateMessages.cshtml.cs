using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Serverless.Forum.Contracts;
using Serverless.Forum.ForumDb;
using Serverless.Forum.Services;
using Serverless.Forum.Utilities;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;

namespace Serverless.Forum.Pages
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

        public List<PrivateMessageDto> InboxMessages { get; private set; }

        public List<PrivateMessageDto> SentMessages { get; private set; }

        public PrivateMessageDto SelectedMessage { get; private set; }

        public bool SelectedMessageIsMine { get; private set; }

        public Paginator InboxPaginator { get; private set; }

        public Paginator SentPaginator { get; private set; }

        private readonly BBCodeRenderingService _renderingService;
        
        public PrivateMessagesModel(ForumDbContext context, ForumTreeService forumService, UserService userService, CacheService cacheService, BBCodeRenderingService renderingService)
            : base(context, forumService, userService, cacheService)
        {
            _renderingService = renderingService;
        }

        public async Task<IActionResult> OnGet()
        {
            var responses = await PageAuthorizationResponses().FirstOrDefaultAsync();
            if (responses != null)
            {
                return responses;
            }

            InboxPaginator = new Paginator(
                count: await _context.PhpbbPrivmsgsTo.CountAsync(x => x.UserId == CurrentUserId && x.AuthorId != x.UserId),
                pageNum: InboxPage ?? 1,
                link: "/PrivateMessages?show=Inbox",
                pageNumKey: nameof(InboxPage)
            );

            SentPaginator = new Paginator(
                count: await _context.PhpbbPrivmsgsTo.CountAsync(x => x.AuthorId == CurrentUserId && x.AuthorId != x.UserId),
                pageNum: SentPage ?? 1,
                link: "/PrivateMessages?show=Sent",
                pageNumKey: nameof(SentPage)
            );

            InboxMessages = await (
                from m in _context.PhpbbPrivmsgs.AsNoTracking()
                
                join mt in _context.PhpbbPrivmsgsTo.AsNoTracking()
                on m.MsgId equals mt.MsgId 
                into joined
                
                from j in joined
                where j.UserId == CurrentUserId && j.AuthorId != j.UserId
                
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
                where j.AuthorId == CurrentUserId && j.AuthorId != j.UserId

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
                SelectedMessageIsMine = to.AuthorId == CurrentUserId;
                var other = await _context.PhpbbUsers.AsNoTracking().FirstOrDefaultAsync(x => x.UserId == (SelectedMessageIsMine ? to.UserId : to.AuthorId));
                SelectedMessage = new PrivateMessageDto
                {
                    MessageId = msg.MsgId,
                    OthersId = other.UserId,
                    OthersName = other?.Username ?? "Anonymous",
                    OthersColor = other?.UserColour,
                    OtherHasAvatar = !string.IsNullOrWhiteSpace(other.UserAvatar),
                    Subject = HttpUtility.HtmlDecode(msg.MessageSubject),
                    Text = await _renderingService.BbCodeToHtml(msg.MessageText, msg.BbcodeUid),
                    Time = msg.MessageTime.ToUtcTime()
                };

                if (to.PmUnread == 1)
                {
                    to.PmUnread = 0;
                    _context.PhpbbPrivmsgsTo.Update(to);
                    await _context.SaveChangesAsync();
                }
            }
            return Page();
        }

        public async Task<IActionResult> OnPost()
        {
            var responses = await PageAuthorizationResponses().FirstOrDefaultAsync();
            if (responses != null)
            {
                return responses;
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
        }
    }
}