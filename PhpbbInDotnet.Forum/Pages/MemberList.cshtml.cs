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
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;

namespace PhpbbInDotnet.Forum.Pages
{
    public class MemberListModel : AuthenticatedPageModel
    {
        public const int PAGE_SIZE = 20;

        private readonly IAnonymousSessionCounter _sessionCounter;

        [BindProperty(SupportsGet = true)]
        public int PageNum { get; set; } = 1;

        [BindProperty(SupportsGet = true)]
        public string? Username { get; set; }

        [BindProperty(SupportsGet = true)]
        public MemberListOrder? Order { get; set; }

        [BindProperty(SupportsGet = true)]
        public MemberListPages Mode { get; set; }

        [BindProperty]
        public string? ValidationDummy { get; set; }

        [BindProperty(SupportsGet = true)]
        public int? GroupId { get; set; }

        public Paginator? Paginator { get; private set; }
        public IEnumerable<PhpbbUsers>? UserList { get; private set; }
        public IEnumerable<PhpbbRanks>? RankList { get; private set; }
        public IEnumerable<PhpbbGroups>? GroupList { get; private set; }
        public bool SearchWasPerformed { get; private set; }
        public IEnumerable<BotData>? BotList { get; private set; }
        public Paginator? BotPaginator { get; private set; }
        public bool CurrentUserIsAdmin { get; private set; }
        public int RegisteredUserCount { get; private set; }

        public MemberListModel(IForumTreeService forumService, IUserService userService, ISqlExecuter sqlExecuter,
            ITranslationProvider translationProvider, IConfiguration config, IAnonymousSessionCounter sessionCounter)
            : base(forumService, userService, sqlExecuter, translationProvider, config)
        {
            _sessionCounter = sessionCounter;
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
                PageNum = Paginator.NormalizePageNumberLowerBound(PageNum);
                GroupList = await SqlExecuter.QueryAsync<PhpbbGroups>("SELECT * FROM phpbb_groups");
                CurrentUserIsAdmin = await UserService.IsAdmin(ForumUser);
                switch (Mode)
                {
                    case MemberListPages.AllUsers:
                        await ResiliencyUtility.RetryOnceAsync(
                            toDo: async () =>
                            {
                                var stmt = "FROM phpbb_users WHERE group_id <> 6 AND user_id <> 1 ";
                                UserList = await SqlExecuter
                                    .WithPagination(PAGE_SIZE * (PageNum - 1), PAGE_SIZE)
                                    .QueryAsync<PhpbbUsers>("SELECT * " + stmt + OrderStatement(Order ?? MemberListOrder.NameAsc));
                                Paginator = new Paginator(await SqlExecuter.ExecuteScalarAsync<int>("SELECT count(1) " + stmt), PageNum, $"/MemberList?order={Order}", PAGE_SIZE, "pageNum");
                                RankList = await SqlExecuter.QueryAsync<PhpbbRanks>("SELECT * FROM phpbb_ranks");
                            },
                            evaluateSuccess: () => UserList!.Any() && PageNum == Paginator!.CurrentPage,
                            fix: () => PageNum = Paginator!.CurrentPage);
                        break;


                    case MemberListPages.SearchUsers:
                        await ResiliencyUtility.RetryOnceAsync(
                            toDo: async () =>
                            {
                                if (!string.IsNullOrWhiteSpace(Username) || GroupId.HasValue)
                                {
                                    var stmt = new StringBuilder("FROM phpbb_users WHERE user_id <> @ANONYMOUS_USER_ID");
                                    var param = new DynamicParameters(new { Constants.ANONYMOUS_USER_ID });

                                    if (!string.IsNullOrWhiteSpace(Username))
                                    {
                                        stmt.AppendLine(" AND username_clean LIKE @username");
                                        param.Add("username", $"%{StringUtility.CleanString(Username)}%");
                                    }
                                    if (GroupId is not null)
                                    {
                                        stmt.AppendLine(" AND group_id = @groupId");
                                        param.Add("groupId", GroupId);
                                    }

                                    var countSql = $"SELECT count(1) {stmt}";
									stmt.AppendLine($" {OrderStatement(Order ?? MemberListOrder.NameAsc)}");
                                    var searchSql = $"SELECT * {stmt}";

									UserList = await SqlExecuter.WithPagination(PAGE_SIZE * (PageNum - 1), PAGE_SIZE).QueryAsync<PhpbbUsers>(searchSql, param);
                                    Paginator = new Paginator(await SqlExecuter.ExecuteScalarAsync<int>(countSql, param), PageNum, $"/MemberList?username={HttpUtility.UrlEncode(Username)}&order={Order}&groupId={GroupId}&handler=search", PAGE_SIZE, "pageNum");
                                    RankList = await SqlExecuter.QueryAsync<PhpbbRanks>("SELECT * FROM phpbb_ranks");
                                    SearchWasPerformed = true;
                                }
                            },
                            evaluateSuccess: () => !SearchWasPerformed || (UserList!.Any() && PageNum == Paginator!.CurrentPage),
                            fix: () => PageNum = Paginator!.CurrentPage);
                        break;

                    case MemberListPages.Groups:
                        break;

                    case MemberListPages.ActiveBots:
                    case MemberListPages.ActiveUsers:
                        var lastVisit = DateTime.UtcNow.Subtract(Configuration.GetValue<TimeSpan?>("UserActivityTrackingInterval") ?? TimeSpan.FromHours(1)).ToUnixTimestamp();
                        var stmt = "FROM phpbb_users WHERE user_lastvisit >= @lastVisit AND user_id <> @ANONYMOUS_USER_ID";
                        var parm = new
                        {
                            lastVisit,
                            Constants.ANONYMOUS_USER_ID
                        };
                        RegisteredUserCount = await SqlExecuter.ExecuteScalarAsync<int>($"SELECT count(1) {stmt}", parm);

                        if (Mode == MemberListPages.ActiveUsers)
                        {
                            await ResiliencyUtility.RetryOnceAsync(
                                toDo: async () =>
                                {

                                    UserList = await SqlExecuter
                                        .WithPagination(PAGE_SIZE * (PageNum - 1), PAGE_SIZE)
                                        .QueryAsync<PhpbbUsers>($"SELECT * {stmt} {OrderStatement(Order ?? MemberListOrder.NameAsc)}", parm);
                                    Paginator = new Paginator(RegisteredUserCount, PageNum, $"/MemberList?handler=setMode&mode={Mode}", PAGE_SIZE, "pageNum");
                                    RankList = await SqlExecuter.QueryAsync<PhpbbRanks>("SELECT * FROM phpbb_ranks");
                                },
                                evaluateSuccess: () => UserList!.Any() && PageNum == Paginator!.CurrentPage,
                                fix: () => PageNum = Paginator!.CurrentPage);
                        }
                        else if (Mode == MemberListPages.ActiveBots)
                        {
                            if (!CurrentUserIsAdmin)
                            {
                                return Unauthorized();
                            }
                            ResiliencyUtility.RetryOnce(
                                toDo: () =>
                                {
                                    BotList = _sessionCounter.GetBots().OrderByDescending(x => x.EntryTime).Skip(PAGE_SIZE * (PageNum - 1)).Take(PAGE_SIZE);
                                    BotPaginator = new Paginator(_sessionCounter.GetActiveBotCount(), PageNum, $"/MemberList?handler=setMode&mode={Mode}", PAGE_SIZE, "pageNum");
                                },
                                evaluateSuccess: () => BotList!.Any() && PageNum == BotPaginator!.CurrentPage,
                                fix: () => PageNum = BotPaginator!.CurrentPage);
                        }
                        break;

                    default:
                        throw new ArgumentException($"Unknown value '{Mode}' for {nameof(Mode)}");
                }

                return Page();
            });

        static string OrderStatement(MemberListOrder order)
            => order switch
            {
                MemberListOrder.NameAsc => "ORDER BY username_clean ASC",
                MemberListOrder.NameDesc => "ORDER BY username_clean DESC",
                MemberListOrder.LastActiveDateAsc => "ORDER BY user_lastvisit ASC",
                MemberListOrder.LastActiveDateDesc => "ORDER BY user_lastvisit DESC",
                MemberListOrder.RegistrationDateAsc => "ORDER BY user_regdate ASC",
                MemberListOrder.RegistrationDateDesc => "ORDER BY user_regdate DESC",
                MemberListOrder.MessageCountAsc => "ORDER BY user_posts ASC",
                MemberListOrder.MessageCountDesc => "ORDER BY user_posts DESC",
                _ => throw new ArgumentException($"Unknown value '{order}'.", nameof(order))
            };

    }
}