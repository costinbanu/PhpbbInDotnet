using Dapper;
using LazyCache;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using PhpbbInDotnet.Database;
using PhpbbInDotnet.Database.Entities;
using PhpbbInDotnet.Languages;
using PhpbbInDotnet.Services;
using PhpbbInDotnet.Utilities;
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

        public MemberListModel(ForumDbContext context, ForumTreeService forumService, UserService userService, IAppCache cache, CommonUtils utils,
            IConfiguration config, LanguageProvider languageProvider)
            : base(context, forumService, userService, cache, utils, languageProvider)
        {
            _config = config;
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
                switch (Mode)
                {
                    case MemberListPages.AllUsers:
                        PageNum = Paginator.NormalizePageNumberLowerBound(PageNum);
                        await Utils.RetryOnce(
                            toDo: async () =>
                            {
                                var userQuery = from u in Context.PhpbbUsers.AsNoTracking()
                                                where u.GroupId != 6
                                                   && u.UserId != Constants.ANONYMOUS_USER_ID
                                                select u;
                                var usersTask = userQuery.OrderUsers(Order ?? MemberListOrder.NameAsc).PaginateUsers(PageNum).ToListAsync();
                                var countTask = userQuery.Distinct().CountAsync(_ => true);
                                var groupsTask = Context.PhpbbGroups.AsNoTracking().ToListAsync();
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
                        PageNum = Paginator.NormalizePageNumberLowerBound(PageNum);
                        await Utils.RetryOnce(
                            toDo: async () =>
                            {
                                var groupsTask = Context.PhpbbGroups.AsNoTracking().ToListAsync();
                                if (!string.IsNullOrWhiteSpace(Username) || GroupId.HasValue)
                                {
                                    var usernameToSearch = Username == null ? null : Utils.CleanString(Username);
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
                        GroupList = await Context.PhpbbGroups.AsNoTracking().ToListAsync();
                        break;

                    case MemberListPages.ActiveBots:
                    case MemberListPages.ActiveUsers:
                        PageNum = Paginator.NormalizePageNumberLowerBound(PageNum);
                        await Utils.RetryOnce(
                            toDo: async () =>
                            {
                                var lastVisit = DateTime.UtcNow.Subtract(_config.GetValue<TimeSpan?>("UserActivityTrackingInterval") ?? TimeSpan.FromHours(1)).ToUnixTimestamp();
                                var userQuery = from u in Context.PhpbbUsers.AsNoTracking()
                                                where u.UserLastvisit >= lastVisit
                                                   && u.UserId != Constants.ANONYMOUS_USER_ID
                                                select u;
                                var userTask = userQuery.OrderUsers(Order ?? MemberListOrder.NameAsc).PaginateUsers(PageNum).ToListAsync();
                                var countTask = userQuery.Distinct().CountAsync(_ => true);
                                var groupsTask = Context.PhpbbGroups.AsNoTracking().ToListAsync();
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