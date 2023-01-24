using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PhpbbInDotnet.Database.Entities;
using PhpbbInDotnet.Domain;
using PhpbbInDotnet.Domain.Extensions;
using PhpbbInDotnet.Domain.Utilities;
using PhpbbInDotnet.Forum.Models;
using PhpbbInDotnet.Objects;
using PhpbbInDotnet.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;

namespace PhpbbInDotnet.Forum.Pages
{
    public class MemberListModel : AuthenticatedPageModel
    {
        public const int PAGE_SIZE = 20;

        private readonly IConfiguration _config;
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

        public MemberListModel(IServiceProvider serviceProvider) : base(serviceProvider)
        {
            _config = serviceProvider.GetRequiredService<IConfiguration>();
            _sessionCounter = serviceProvider.GetRequiredService<IAnonymousSessionCounter>();
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
                var groupsTask = Context.PhpbbGroups.AsNoTracking().ToListAsync();
                switch (Mode)
                {
                    case MemberListPages.AllUsers:
                        await ResiliencyUtility.RetryOnceAsync(
                            toDo: async () =>
                            {
                                var userQuery = from u in Context.PhpbbUsers.AsNoTracking()
                                                where u.GroupId != 6
                                                   && u.UserId != Constants.ANONYMOUS_USER_ID
                                                select u;
                                var usersTask = userQuery.OrderUsers(Order ?? MemberListOrder.NameAsc).PaginateUsers(PageNum).ToListAsync();
                                var countTask = userQuery.Distinct().CountAsync(_ => true);
                                var ranksTask = Context.PhpbbRanks.AsNoTracking().ToListAsync();
                                await Task.WhenAll(usersTask, countTask, groupsTask, ranksTask);
                                UserList = await usersTask;
                                Paginator = new Paginator(await countTask, PageNum, $"/MemberList?order={Order}", PAGE_SIZE, "pageNum");
                                GroupList = await groupsTask;
                                RankList = await ranksTask;
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
                                    var usernameToSearch = Username == null ? null : StringUtility.CleanString(Username);
                                    var searchQuery = from u in Context.PhpbbUsers.AsNoTracking()
                                                      where (usernameToSearch == null || u.UsernameClean.Contains(usernameToSearch))
                                                         && (GroupId == null || u.GroupId == GroupId)
                                                         && u.UserId != Constants.ANONYMOUS_USER_ID
                                                      select u;
                                    var searchTask = searchQuery.OrderUsers(Order ?? MemberListOrder.NameAsc).PaginateUsers(PageNum).ToListAsync();
                                    var countTask = searchQuery.Distinct().CountAsync(_ => true);
                                    var ranksTask = Context.PhpbbRanks.AsNoTracking().ToListAsync();
                                    await Task.WhenAll(searchTask, countTask, ranksTask, groupsTask);
                                    UserList = await searchTask;
                                    Paginator = new Paginator(await countTask, PageNum, $"/MemberList?username={HttpUtility.UrlEncode(Username)}&order={Order}&groupId={GroupId}&handler=search", PAGE_SIZE, "pageNum");
                                    RankList = await ranksTask;
                                    SearchWasPerformed = true;
                                }
                                GroupList = await groupsTask;
                            },
                            evaluateSuccess: () => !SearchWasPerformed || (UserList!.Any() && PageNum == Paginator!.CurrentPage),
                            fix: () => PageNum = Paginator!.CurrentPage);
                        break;

                    case MemberListPages.Groups:
                        GroupList = await groupsTask;
                        break;

                    case MemberListPages.ActiveBots:
                        await ResiliencyUtility.RetryOnceAsync(
                            toDo: async () =>
                            {
                                BotList = _sessionCounter.GetBots().OrderByDescending(x => x.EntryTime).Skip(PAGE_SIZE * (PageNum - 1)).Take(PAGE_SIZE);
                                BotPaginator = new Paginator(_sessionCounter.GetActiveBotCount(), PageNum, $"/MemberList?handler=setMode&mode={Mode}", PAGE_SIZE, "pageNum");
                                GroupList = await groupsTask;
                            },
                            evaluateSuccess: () => BotList!.Any() && PageNum == BotPaginator!.CurrentPage,
                            fix: () => PageNum = BotPaginator!.CurrentPage);
                        break;

                    case MemberListPages.ActiveUsers:
                        await ResiliencyUtility.RetryOnceAsync(
                            toDo: async () =>
                            {
                                var lastVisit = DateTime.UtcNow.Subtract(_config.GetValue<TimeSpan?>("UserActivityTrackingInterval") ?? TimeSpan.FromHours(1)).ToUnixTimestamp();
                                var userQuery = from u in Context.PhpbbUsers.AsNoTracking()
                                                where u.UserLastvisit >= lastVisit
                                                   && u.UserId != Constants.ANONYMOUS_USER_ID
                                                select u;
                                var userTask = userQuery.OrderUsers(Order ?? MemberListOrder.NameAsc).PaginateUsers(PageNum).ToListAsync();
                                var countTask = userQuery.Distinct().CountAsync(_ => true);
                                var ranksTask = Context.PhpbbRanks.AsNoTracking().ToListAsync();
                                await Task.WhenAll(userTask, countTask, groupsTask, ranksTask);
                                UserList = await userTask;
                                Paginator = new Paginator(await countTask, PageNum, $"/MemberList?handler=setMode&mode={Mode}", PAGE_SIZE, "pageNum");
                                GroupList = await groupsTask;
                                RankList = await ranksTask;
                            },
                            evaluateSuccess: () => UserList!.Any() && PageNum == Paginator!.CurrentPage,
                            fix: () => PageNum = Paginator!.CurrentPage);
                        break;

                    default:
                        throw new ArgumentException($"Unknown value '{Mode}' for {nameof(Mode)}");
                }

                return Page();
            });
    }

    static class IQueryableExtensions
    {
        internal static IQueryable<PhpbbUsers> OrderUsers(this IQueryable<PhpbbUsers> query, MemberListOrder order)
            => order switch
            {
                MemberListOrder.NameAsc => query.OrderBy(u => u.UsernameClean),
                MemberListOrder.NameDesc => query.OrderByDescending(u => u.UsernameClean),
                MemberListOrder.LastActiveDateAsc => query.OrderBy(u => u.UserLastvisit),
                MemberListOrder.LastActiveDateDesc => query.OrderByDescending(u => u.UserLastvisit),
                MemberListOrder.RegistrationDateAsc => query.OrderBy(u => u.UserRegdate),
                MemberListOrder.RegistrationDateDesc => query.OrderByDescending(u => u.UserRegdate),
                MemberListOrder.MessageCountAsc => query.OrderBy(u => u.UserPosts),
                MemberListOrder.MessageCountDesc => query.OrderByDescending(u => u.UserPosts),
                _ => throw new ArgumentException($"Unknown value '{order}' in {nameof(OrderUsers)}.", nameof(order))
            };

        internal static IQueryable<PhpbbUsers> PaginateUsers(this IQueryable<PhpbbUsers> query, int pageNum)
            => query.Skip(MemberListModel.PAGE_SIZE * (pageNum - 1)).Take(MemberListModel.PAGE_SIZE);
    }
}