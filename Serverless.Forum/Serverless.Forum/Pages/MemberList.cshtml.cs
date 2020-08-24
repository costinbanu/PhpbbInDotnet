using Dapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Serverless.Forum.ForumDb;
using Serverless.Forum.ForumDb.Entities;
using Serverless.Forum.Services;
using Serverless.Forum.Utilities;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Web;

namespace Serverless.Forum.Pages
{
    public class MemberListModel : ModelWithLoggedUser
    {
        
        const int PAGE_SIZE = 20;
        private readonly Utils _utils;

        [BindProperty(SupportsGet = true)]
        public int PageNum { get; set; } = 1;

        [BindProperty(SupportsGet = true)]
        public string Username { get; set; }

        [BindProperty(SupportsGet = true)]
        public MemberListOrder? Order { get; set; }

        public Paginator UserPaginator { get; private set; }
        public Paginator SearchPaginator { get; private set; }
        public MemberListPages Mode { get; private set; }
        public IEnumerable<PhpbbUsers> UserList { get; private set; }
        public IEnumerable<PhpbbGroups> UserGroupList { get; private set; }
        public IEnumerable<PhpbbRanks> UserRankList { get; private set; }
        public IEnumerable<PhpbbUsers> SearchResults { get; private set; }
        public IEnumerable<PhpbbGroups> SearchGroupList { get; private set; }
        public IEnumerable<PhpbbRanks> SearchRankList { get; private set; }
        public IEnumerable<PhpbbGroups> GroupList { get; private set; }

        public MemberListModel(ForumDbContext context, ForumTreeService forumService, UserService userService, CacheService cacheService, Utils utils, IConfiguration config) 
            : base (context, forumService, userService, cacheService, config) 
        {
            _utils = utils;
        }

        public async Task<IActionResult> OnGet()
            => await WithRegisteredUser(async () =>
            {
                Mode = MemberListPages.AllUsers;
                using (var connection = _context.Database.GetDbConnection())
                {
                    await connection.OpenIfNeeded();
                    UserList = await connection.QueryAsync<PhpbbUsers>(
                        $"SELECT * FROM phpbb_users ORDER BY {GetOrder(Order ?? MemberListOrder.NameAsc)} LIMIT @skip, @take",
                        new { skip = PAGE_SIZE * (PageNum - 1), take = PAGE_SIZE }
                    );
                    (UserGroupList, UserRankList) = await GetSpecificDependencies(connection, UserList);
                }
                UserPaginator = new Paginator(await _context.PhpbbUsers.AsNoTracking().CountAsync(), PageNum, $"/MemberList?order={Order}", PAGE_SIZE, "pageNum");
                return Page();
            });

        public async Task OnGetSearch()
            => await WithRegisteredUser(async () =>
            {
                Mode = MemberListPages.SearchUsers;
                using var connection = _context.Database.GetDbConnection();
                await connection.OpenIfNeeded();
                var results = (await connection.QueryAsync<PhpbbUsers>(
                    $"SELECT * FROM phpbb_users WHERE CONVERT(LOWER(username) USING utf8) LIKE @search ORDER BY {GetOrder(Order ?? MemberListOrder.NameAsc)}",
                    new { search = $"%{_utils.CleanString(Username)}%", order = GetOrder(Order ?? MemberListOrder.NameAsc) }
                )).ToList();
                SearchResults = results.Skip(PAGE_SIZE * (PageNum - 1)).Take(PAGE_SIZE);
                (SearchGroupList, SearchRankList) = await GetSpecificDependencies(connection, results);
                SearchPaginator = new Paginator(results.Count, PageNum, $"/MemberList?username={HttpUtility.UrlEncode(Username)}&order={Order}&handler=search", PAGE_SIZE, "pageNum");
                return Page();
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

        private async Task<(IEnumerable<PhpbbGroups> specificGroups, IEnumerable<PhpbbRanks> specificRanks)> GetSpecificDependencies(IDbConnection connection, IEnumerable<PhpbbUsers> userSource)
        {
            using var multi = await connection.QueryMultipleAsync(
                @"SELECT * FROM phpbb_groups WHERE group_id IN @groups;
                  SELECT * FROM phpbb_ranks WHERE rank_id IN @ranks;
                  SELECT * FROM phpbb_groups;",
                new
                {
                    groups = userSource.Select(x => x.GroupId).Distinct().DefaultIfEmpty(),
                    ranks = userSource.Select(x => x.UserRank).Distinct().DefaultIfEmpty()
                }
            );
            var specificGroups = await multi.ReadAsync<PhpbbGroups>();
            var specificRanks = await multi.ReadAsync<PhpbbRanks>();
            GroupList = await multi.ReadAsync<PhpbbGroups>();
            return (specificGroups, specificRanks);
        }
    }
}