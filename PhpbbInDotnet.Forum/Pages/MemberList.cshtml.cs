using Dapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using PhpbbInDotnet.Database;
using PhpbbInDotnet.Database.Entities;
using PhpbbInDotnet.Services;
using PhpbbInDotnet.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;

namespace PhpbbInDotnet.Forum.Pages
{
    public class MemberListModel : ModelWithLoggedUser
    {
        const int PAGE_SIZE = 20;
        private readonly WritingToolsService _writingService;

        [BindProperty(SupportsGet = true)]
        public int PageNum { get; set; } = 1;

        [BindProperty(SupportsGet = true)]
        public string Username { get; set; }

        [BindProperty(SupportsGet = true)]
        public MemberListOrder? Order { get; set; }

        [BindProperty(SupportsGet = true)]
        public MemberListPages Mode { get; set; }

        [BindProperty]
        public string ValidationDummy { get; set; }

        public Paginator Paginator { get; private set; }
        public IEnumerable<PhpbbUsers> UserList { get; private set; }
        public IEnumerable<PhpbbRanks> RankList { get; private set; }
        public IEnumerable<PhpbbGroups> GroupList { get; private set; }
        public bool SearchWasPerformed { get; private set; }

        public MemberListModel(ForumDbContext context, ForumTreeService forumService, UserService userService, CacheService cacheService, CommonUtils utils, 
            IConfiguration config, WritingToolsService writingService, AnonymousSessionCounter sessionCounter) 
            : base (context, forumService, userService, cacheService, config, sessionCounter, utils) 
        {
            _writingService = writingService;
        }

        public async Task<IActionResult> OnGet()
        {
            Mode = MemberListPages.AllUsers;
            return await OnGetSetMode();
        }

        public async Task<IActionResult> OnGetSearch()
        {
            Mode = MemberListPages.SearchUsers;
            return await OnGetSetMode();
        }

        public async Task<IActionResult> OnGetSetMode()
            => await WithRegisteredUser(async (_) =>
            {
                using var connection = _context.Database.GetDbConnection();
                await connection.OpenIfNeededAsync();
                switch (Mode)
                {
                    case MemberListPages.AllUsers:
                        using (var multi = await connection.QueryMultipleAsync(
                            $"SELECT * FROM phpbb_users ORDER BY {GetOrder(Order ?? MemberListOrder.NameAsc)} LIMIT @skip, @take;" +
                            $"SELECT count(distinct user_id) FROM phpbb_users;" +
                            $"SELECT * FROM phpbb_groups;" +
                            $"SELECT * FROM phpbb_ranks;",
                            new { skip = PAGE_SIZE * (PageNum - 1), take = PAGE_SIZE }
                        ))
                        {
                            UserList = await multi.ReadAsync<PhpbbUsers>();
                            Paginator = new Paginator(unchecked((int)await multi.ReadSingleAsync<long>()), PageNum, $"/MemberList?order={Order}", PAGE_SIZE, "pageNum");
                            GroupList = await multi.ReadAsync<PhpbbGroups>();
                            RankList = await multi.ReadAsync<PhpbbRanks>();
                            break;
                        }
                    case MemberListPages.SearchUsers:
                        if (!string.IsNullOrWhiteSpace(Username))
                        {
                            using var multi = await connection.QueryMultipleAsync(
                                $"SELECT * FROM phpbb_users WHERE CONVERT(LOWER(username) USING utf8) LIKE @search ORDER BY {GetOrder(Order ?? MemberListOrder.NameAsc)} LIMIT @skip, @take;" +
                                $"SELECT count(distinct user_id) FROM phpbb_users WHERE CONVERT(LOWER(username) USING utf8) LIKE @search;" +
                                $"SELECT * FROM phpbb_groups;" +
                                $"SELECT * FROM phpbb_ranks;",
                                new { search = $"%{_utils.CleanString(Username)}%", skip = PAGE_SIZE * (PageNum - 1), take = PAGE_SIZE }
                            );
                            UserList = await multi.ReadAsync<PhpbbUsers>();
                            Paginator = new Paginator(unchecked((int)await multi.ReadSingleAsync<long>()), PageNum, $"/MemberList?username={HttpUtility.UrlEncode(Username)}&order={Order}&handler=search", PAGE_SIZE, "pageNum");
                            GroupList = await multi.ReadAsync<PhpbbGroups>();
                            RankList = await multi.ReadAsync<PhpbbRanks>();
                            SearchWasPerformed = true;
                        }
                        break;
                    case MemberListPages.Groups:
                        GroupList = await connection.QueryAsync<PhpbbGroups>("SELECT * FROM phpbb_groups");
                        break;
                    case MemberListPages.ActiveUsers:
                        using (var multi = await connection.QueryMultipleAsync(
                            $"SELECT * FROM phpbb_users WHERE user_lastvisit >= @lastVisit ORDER BY {GetOrder(Order ?? MemberListOrder.NameAsc)} LIMIT @skip, @take;" +
                            $"SELECT count(distinct user_id) FROM phpbb_users WHERE user_lastvisit >= @lastVisit;" +
                            $"SELECT * FROM phpbb_groups;" +
                            $"SELECT * FROM phpbb_ranks;",
                            new { lastVisit = DateTime.UtcNow.Subtract(TimeSpan.FromMinutes(_config.GetValue<int?>("UserActivityTrackingIntervalMinutes") ?? 60)).ToUnixTimestamp(), skip = PAGE_SIZE * (PageNum - 1), take = PAGE_SIZE }
                        ))
                        {
                            UserList = await multi.ReadAsync<PhpbbUsers>();
                            Paginator = new Paginator(unchecked((int)await multi.ReadSingleAsync<long>()), PageNum, $"/MemberList?handler=setMode&mode={Mode}", PAGE_SIZE, "pageNum");
                            GroupList = await multi.ReadAsync<PhpbbGroups>();
                            RankList = await multi.ReadAsync<PhpbbRanks>();
                            break;
                        }
                    default:
                        throw new ArgumentException($"Unknown value '{Mode}' for {nameof(Mode)}");
                }

                return Page();
            });

        public async Task<IActionResult> OnPostEditGroup(int groupId, string groupName, string groupDesc, string groupColor, int groupEditTime)
            => await WithAdmin(async () =>
            {
                Mode = MemberListPages.Groups;
                var group = await _context.PhpbbGroups.FirstOrDefaultAsync(g => g.GroupId == groupId);
                if (group == null)
                {
                    ModelState.AddModelError(nameof(ValidationDummy), $"Grupul {groupId} nu există.");
                    return await OnGetSetMode();
                }

                group.GroupName = HttpUtility.HtmlEncode(groupName);
                group.GroupDesc = _writingService.PrepareTextForSaving(groupDesc);
                group.GroupColour = (groupColor ?? string.Empty).Trim('#').ToUpperInvariant();
                group.GroupEditTime = groupEditTime;

                await _context.SaveChangesAsync();

                return await OnGetSetMode();
            });

        public string GetOrderDisplayName(MemberListOrder order)
            => order switch
            {
                MemberListOrder.NameAsc => "Nume utilizator A → Z",
                MemberListOrder.NameDesc => "Nume utilizator Z → A",
                MemberListOrder.LastActiveDateAsc => "Activ ultima oară (crescător)",
                MemberListOrder.LastActiveDateDesc => "Activ ultima oară (descrescător)",
                MemberListOrder.RegistrationDateAsc => "Data înregistrării (crescător)",
                MemberListOrder.RegistrationDateDesc => "Data înregistrării (descrescător)",
                _ => throw new ArgumentException($"Unknown value '{order}' in {nameof(GetOrderDisplayName)}.", nameof(order))
            };

        private string GetOrder(MemberListOrder order)
            => order switch
            {
                MemberListOrder.NameAsc => "username asc",
                MemberListOrder.NameDesc => "username desc",
                MemberListOrder.LastActiveDateAsc => "user_lastvisit asc",
                MemberListOrder.LastActiveDateDesc => "user_lastvisit desc",
                MemberListOrder.RegistrationDateAsc => "user_regdate asc",
                MemberListOrder.RegistrationDateDesc => "user_regdate desc",
                _ => throw new ArgumentException($"Unknown value '{order}' in {nameof(GetOrder)}.", nameof(order))
            };
    }
}