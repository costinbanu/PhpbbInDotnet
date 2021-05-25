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
using System.Threading.Tasks;
using System.Web;

namespace PhpbbInDotnet.Forum.Pages
{
    public class MemberListModel : AuthenticatedPageModel
    {
        public const int PAGE_SIZE = 20;

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

        [BindProperty(SupportsGet = true)]
        public int? GroupId { get; set; }

        public Paginator Paginator { get; private set; }
        public IEnumerable<PhpbbUsers> UserList { get; private set; }
        public IEnumerable<PhpbbRanks> RankList { get; private set; }
        public IEnumerable<PhpbbGroups> GroupList { get; private set; }
        public bool SearchWasPerformed { get; private set; }

        public MemberListModel(ForumDbContext context, ForumTreeService forumService, UserService userService, IAppCache cache, CommonUtils utils, 
            IConfiguration config, WritingToolsService writingService, AnonymousSessionCounter sessionCounter, LanguageProvider languageProvider) 
            : base (context, forumService, userService, cache, config, sessionCounter, utils, languageProvider) 
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
                var connection = await Context.GetDbConnectionAsync();
                switch (Mode)
                {
                    case MemberListPages.AllUsers:
                        using (var multi = await connection.QueryMultipleAsync(
                            $"SELECT * FROM phpbb_users WHERE group_id <> 6 AND user_id <> @ANONYMOUS_USER_ID ORDER BY {GetOrder(Order ?? MemberListOrder.NameAsc)} LIMIT @skip, @take; " +
                            $"SELECT count(distinct user_id) FROM phpbb_users WHERE group_id <> 6 AND user_id <> @ANONYMOUS_USER_ID; " +
                            $"SELECT * FROM phpbb_groups; " +
                            $"SELECT * FROM phpbb_ranks;",
                            new { skip = PAGE_SIZE * (PageNum - 1), take = PAGE_SIZE, Constants.ANONYMOUS_USER_ID }
                        ))
                        {
                            UserList = await multi.ReadAsync<PhpbbUsers>();
                            Paginator = new Paginator(unchecked((int)await multi.ReadSingleAsync<long>()), PageNum, $"/MemberList?order={Order}", PAGE_SIZE, "pageNum");
                            GroupList = await multi.ReadAsync<PhpbbGroups>();
                            RankList = await multi.ReadAsync<PhpbbRanks>();
                            break;
                        }
                    case MemberListPages.SearchUsers:
                        if (!string.IsNullOrWhiteSpace(Username) || GroupId.HasValue)
                        {
                            var whereClause =
                                @"(@search IS NULL OR username_clean LIKE @search)
                                AND (@groupId IS NULL OR group_id = @groupId)
                                AND user_id <> @ANONYMOUS_USER_ID";

                            using var multi = await connection.QueryMultipleAsync(
                                $@"SELECT * 
                                     FROM phpbb_users 
                                    WHERE {whereClause}
                                    ORDER BY {GetOrder(Order ?? MemberListOrder.NameAsc)} 
                                    LIMIT @skip, @take; 

                                   SELECT count(distinct user_id) 
                                     FROM phpbb_users 
                                    WHERE {whereClause}; 

                                   SELECT * 
                                     FROM phpbb_ranks;",
                                new 
                                { 
                                    search = Username == null ? null : $"%{Utils.CleanString(Username)}%", 
                                    GroupId, 
                                    skip = PAGE_SIZE * (PageNum - 1), 
                                    take = PAGE_SIZE, Constants.ANONYMOUS_USER_ID 
                                }
                            );
                            UserList = await multi.ReadAsync<PhpbbUsers>();
                            Paginator = new Paginator(unchecked((int)await multi.ReadSingleAsync<long>()), PageNum, $"/MemberList?username={HttpUtility.UrlEncode(Username)}&order={Order}&groupId={GroupId}&handler=search", PAGE_SIZE, "pageNum");
                            RankList = await multi.ReadAsync<PhpbbRanks>();
                            SearchWasPerformed = true;
                        }
                        GroupList = await connection.QueryAsync<PhpbbGroups>("SELECT * FROM phpbb_groups");
                        break;
                    case MemberListPages.Groups:
                        GroupList = await connection.QueryAsync<PhpbbGroups>("SELECT * FROM phpbb_groups");
                        break;
                    case MemberListPages.ActiveBots:
                    case MemberListPages.ActiveUsers:
                        using (var multi = await connection.QueryMultipleAsync(
                            $"SELECT * FROM phpbb_users WHERE user_lastvisit >= @lastVisit AND user_id <> @ANONYMOUS_USER_ID ORDER BY {GetOrder(Order ?? MemberListOrder.NameAsc)} LIMIT @skip, @take;" +
                            $"SELECT count(distinct user_id) FROM phpbb_users WHERE user_lastvisit >= @lastVisit AND user_id <> @ANONYMOUS_USER_ID;" +
                            $"SELECT * FROM phpbb_groups;" +
                            $"SELECT * FROM phpbb_ranks;",
                            new { lastVisit = DateTime.UtcNow.Subtract(Config.GetValue<TimeSpan?>("UserActivityTrackingInterval") ?? TimeSpan.FromHours(1)).ToUnixTimestamp(), skip = PAGE_SIZE * (PageNum - 1), take = PAGE_SIZE, Constants.ANONYMOUS_USER_ID }
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

        private string GetOrder(MemberListOrder order)
            => order switch
            {
                MemberListOrder.NameAsc => "username_clean asc",
                MemberListOrder.NameDesc => "username_clean desc",
                MemberListOrder.LastActiveDateAsc => "user_lastvisit asc",
                MemberListOrder.LastActiveDateDesc => "user_lastvisit desc",
                MemberListOrder.RegistrationDateAsc => "user_regdate asc",
                MemberListOrder.RegistrationDateDesc => "user_regdate desc",
                MemberListOrder.MessageCountAsc => "user_posts asc",
                MemberListOrder.MessageCountDesc => "user_posts desc",
                _ => throw new ArgumentException($"Unknown value '{order}' in {nameof(GetOrder)}.", nameof(order))
            };
    }
}