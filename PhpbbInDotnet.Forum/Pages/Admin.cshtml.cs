using Dapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Configuration;
using PhpbbInDotnet.Database.Entities;
using PhpbbInDotnet.Database.SqlExecuter;
using PhpbbInDotnet.Domain;
using PhpbbInDotnet.Forum.Models;
using PhpbbInDotnet.Languages;
using PhpbbInDotnet.Objects;
using PhpbbInDotnet.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Mime;
using System.Threading.Tasks;
using System.Web;
using IOFile = System.IO.File;

namespace PhpbbInDotnet.Forum.Pages
{
	[RequestSizeLimit(10 * 1024 * 1024)]
    public partial class AdminModel : AuthenticatedPageModel
    {
        [BindProperty]
        public AdminUserSearch? SearchParameters { get; set; }
        public List<PhpbbUsers> UserSearchResults { get; private set; }
        public List<PhpbbUsers>? InactiveUsers { get; private set; }
        public List<PhpbbUsers>? ActiveUsersWithUnconfirmedEmail { get; private set; }
        public List<UpsertGroupDto>? Groups { get; private set; }
        public List<PhpbbRanks>? Ranks { get; private set; }
        public List<UpsertBanListDto>? BanList { get; private set; }
        public List<SelectListItem>? RankListItems { get; private set; }
        public List<SelectListItem>? RoleListItems { get; private set; }
        public bool WasSearchPerformed { get; private set; } = false;
        [BindProperty]
        public PhpbbForums? Forum { get; set; } = null;
        [BindProperty]
        public int? ParentForumId { get; set; } = null;
        public List<PhpbbForums> ForumChildren { get; private set; }
        public List<SelectListItem> ForumSelectedParent { get; private set; }
        public IEnumerable<ForumPermissions>? Permissions { get; private set; }
        public bool ShowForum { get; private set; }
        public bool IsRootForum { get; private set; }
        public List<PhpbbWords>? BannedWords { get; set; }
        public List<PhpbbBbcodes>? CustomBbCodes { get; set; }
        public List<PhpbbSmilies>? Smilies { get; set; }
        [BindProperty(SupportsGet = true)]
        public OperationLogType? LogType { get; set; }
        [BindProperty(SupportsGet = true)]
        public int LogPage { get; set; } = 1;
        [BindProperty(SupportsGet = true)]
        public string? AuthorName { get; set; }
        [BindProperty]
        public string? SystemLogPath { get; set; }
        public List<OperationLogSummary>? CurrentLogItems { get; private set; }
        public int TotalLogItemCount { get; private set; }
        public List<(DateTime LogDate, string? LogPath)>? SystemLogs { get; private set; }


        public AdminCategories Category { get; private set; } = AdminCategories.Users;
        public bool? IsSuccess { get; private set; }
        public string? Message { get; private set; }
        public string MessageClass
            => IsSuccess switch
            {
                null => "message",
                true when string.IsNullOrWhiteSpace(Message) => "message",
                true => "message success",
                _ => "message fail",
            };

        private readonly IAdminUserService _adminUserService;
        private readonly IAdminForumService _adminForumService;
        private readonly IWritingToolsService _adminWritingService;
        private readonly IOperationLogService _logService;

        public AdminModel(IAdminUserService adminUserService, IAdminForumService adminForumService, IWritingToolsService adminWritingService, 
            IOperationLogService logService, IForumTreeService forumService, IUserService userService,
            ISqlExecuter sqlExecuter, ITranslationProvider translationProvider, IConfiguration configuration)
            : base(forumService, userService, sqlExecuter, translationProvider, configuration)
        {
            _adminUserService = adminUserService;
            _adminForumService = adminForumService;
            _adminWritingService = adminWritingService;
            _logService = logService;
            UserSearchResults = new List<PhpbbUsers>();
            ForumChildren = new List<PhpbbForums>();
            ForumSelectedParent = new List<SelectListItem>();
        }

        public async Task<IActionResult> OnGet()
            => await WithAdmin(async () =>
            {
                var inactiveUsersTask = _adminUserService.GetInactiveUsers();
                var activeUsersWithUnconfirmedEmailTask = _adminUserService.GetActiveUsersWithUnconfirmedEmail();
                var groupsTask = _adminUserService.GetGroups();
                var ranksTask = SqlExecuter.QueryAsync<PhpbbRanks>("SELECT * FROM phpbb_ranks");
                var banListTask = _adminUserService.GetBanList();
                var rankListTask = _adminUserService.GetRanksSelectListItems();
                var roleListTask = _adminUserService.GetRolesSelectListItems();

                var bannedWordsTask = SqlExecuter.QueryAsync<PhpbbWords>("SELECT * FROM phpbb_words");
                var customBbCodesTask = SqlExecuter.QueryAsync<PhpbbBbcodes>("SELECT * FROM phpbb_bbcodes");
                var smiliesTask = _adminWritingService.GetSmilies();

                var logsTask = _logService.GetOperationLogs(LogType, AuthorName, LogPage);
                var systemLogsTask = Task.Run(() => _logService.GetSystemLogs() ?? new List<(DateTime LogDate, string? LogPath)>());
                
                await Task.WhenAll(inactiveUsersTask, activeUsersWithUnconfirmedEmailTask, groupsTask, ranksTask, banListTask, 
                    rankListTask, roleListTask, bannedWordsTask, customBbCodesTask, smiliesTask, logsTask, systemLogsTask);
                
                InactiveUsers = await inactiveUsersTask;
                ActiveUsersWithUnconfirmedEmail = await activeUsersWithUnconfirmedEmailTask;
                Groups = await groupsTask;
                Ranks = (await ranksTask).AsList();
                BanList = await banListTask;
                RankListItems = await rankListTask;
                RoleListItems = await roleListTask;

                BannedWords = (await bannedWordsTask).AsList();
                CustomBbCodes = (await customBbCodesTask).AsList();
                Smilies = await smiliesTask;

                (CurrentLogItems, TotalLogItemCount) = await logsTask;
                SystemLogs = await systemLogsTask;

                return Page();
            });

        #region Admin user

        public Task<IActionResult> OnGetUserSearch(int userId)
        {
            SearchParameters = new AdminUserSearch { UserId= userId };
            return OnPostUserSearch();
        }

        public Task<IActionResult> OnPostUserSearch()
            => WithAdmin(async () =>
            {
                Category = AdminCategories.Users;
                WasSearchPerformed = true;
                var lang = Language;
                if (new[] { SearchParameters?.Username, SearchParameters?.Email, SearchParameters?.RegisteredFrom, SearchParameters?.RegisteredTo, SearchParameters?.LastActiveFrom, SearchParameters?.LastActiveTo }.All(string.IsNullOrWhiteSpace) 
                    && ((SearchParameters?.UserId ?? 0) == 0) 
                    && !(SearchParameters?.NeverActive ?? false))
                {
                    Message = TranslationProvider.Admin[lang, "TOO_FEW_SEARCH_CRITERIA"];
                    IsSuccess = false;
                    return await OnGet();
                }

                (Message, IsSuccess, UserSearchResults) = await _adminUserService.UserSearchAsync(SearchParameters);
                if (!UserSearchResults.Any())
                {
                    Message = TranslationProvider.BasicText[lang, "NO_RESULTS_FOUND"];
                    IsSuccess = false;
                }
                return await OnGet();
            });

        public Task<IActionResult> OnPostUserManagement(AdminUserActions? userAction, int? userId)
            => WithAdmin(async () =>
            {
                (Message, IsSuccess) = await _adminUserService.ManageUser(userAction, userId, ForumUser.UserId);
                Category = AdminCategories.Users;
                return await OnGet();
            });

        public Task<IActionResult> OnPostBatchUserManagement(int[] userIds)
            => WithAdmin(async () =>
            {
                (Message, IsSuccess) = await _adminUserService.DeleteUsersWithEmailNotConfirmed(userIds, ForumUser.UserId);
                Category = AdminCategories.Users;
                return await OnGet();
            });

        public Task<IActionResult> OnPostGroupManagement(UpsertGroupDto dto)
            => WithAdmin(async () =>
            {
                (Message, IsSuccess) = await _adminUserService.ManageGroup(dto, ForumUser.UserId);
                Category = AdminCategories.Users;
                return await OnGet();
            });

        public Task<IActionResult> OnPostRankManagement(int? rankId, string rankName, bool? deleteRank)
            => WithAdmin(async () =>
            {
                (Message, IsSuccess) = await _adminUserService.ManageRank(rankId, rankName, deleteRank, ForumUser.UserId);
                Category = AdminCategories.Users;
                return await OnGet();
            });

        public Task<IActionResult> OnPostBanUser(List<UpsertBanListDto> banlist, List<int> toRemove)
            => WithAdmin(async () =>
            {
                (Message, IsSuccess) = await _adminUserService.BanUser(banlist, toRemove, ForumUser.UserId);
                Category = AdminCategories.Users;
                return await OnGet();
            });

        #endregion Admin user

        #region Admin forum

        public Task<IActionResult> OnPostShowForum(int? forumId)
            => WithAdmin(async () =>
            {
                IsRootForum = forumId == 0;

                if (forumId != null)
                {
                    var permissionsTask = _adminForumService.GetPermissions(forumId.Value);
                    var showForumTask = _adminForumService.ShowForum(forumId.Value);
                    await Task.WhenAll(permissionsTask, showForumTask);
                    Permissions = await permissionsTask;
                    (Forum, ForumChildren) = await showForumTask;
                }

                ShowForum = true;
                Category = AdminCategories.Forums;
                return await OnGet();
            });

        public Task<IActionResult> OnPostForumManagement(UpsertForumDto dto)
            => WithAdmin(async () =>
            {
                var result = await _adminForumService.ManageForumsAsync(dto, ForumUser.UserId, dto.IsRoot);

                Message = result.Message;
                IsSuccess = result.IsSuccess;
                Forum = result.Forum;
                ShowForum = false;
                Category = AdminCategories.Forums;
                return await OnGet();
            });

        public Task<IActionResult> OnPostDeleteForum(int forumId)
            => WithAdmin(async () =>
            {
                (Message, IsSuccess) = await _adminForumService.DeleteForum(forumId, ForumUser.UserId);

                ShowForum = false;
                Category = AdminCategories.Forums;
                return await OnGet();
            });

        #endregion Admin forum

        #region Admin writing

        public Task<IActionResult> OnPostBanWords(List<PhpbbWords> words, List<int> toRemove)
            => WithAdmin(async () =>
            {
                (Message, IsSuccess) = await _adminWritingService.ManageBannedWords(words, toRemove);
                Category = AdminCategories.WritingTools;
                return await OnGet();
            });

        public Task<IActionResult> OnPostBBCodes(List<PhpbbBbcodes> codes, List<int> toRemove, List<int> toDisplay)
            => WithAdmin(async () =>
            {
                (Message, IsSuccess) = await _adminWritingService.ManageBBCodes(codes, toRemove, toDisplay);
                Category = AdminCategories.WritingTools;
                return await OnGet();
            });

        public Task<IActionResult> OnPostSmilies(List<UpsertSmiliesDto> dto, List<string> newOrder, List<int> codesToDelete, List<string> smileyGroupsToDelete)
            => WithAdmin(async () =>
            {
                (Message, IsSuccess) = await _adminWritingService.ManageSmilies(dto, newOrder, codesToDelete, smileyGroupsToDelete);
                var x = dto;
                var y = codesToDelete;
                var z = smileyGroupsToDelete;
                var t = newOrder;
                Category = AdminCategories.WritingTools;
                return await OnGet();
            });

        #endregion Admin writing

        #region Logs

        public async Task<IActionResult> OnGetForumLogs()
        {
            Category = AdminCategories.Logs;
            return await OnGet();
        }

        public async Task<IActionResult> OnPostSystemLogs()
        {
            if (string.IsNullOrWhiteSpace(SystemLogPath))
            {
                return await OnGet();
            }

            return await WithAdmin(async () =>
            {
                var originalFileName = Path.GetFileName(SystemLogPath)!;
                var filePattern = $"{Path.GetFileNameWithoutExtension(originalFileName)}*";
                var directory = Path.GetDirectoryName(SystemLogPath)!;

                var toReturn = new MemoryStream();
                foreach (var file in Directory.EnumerateFiles(directory, filePattern))
                {
                    using var fileStream = IOFile.Open(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    await fileStream.CopyToAsync(toReturn);
                }
                await toReturn.FlushAsync();
                toReturn.Seek(0, SeekOrigin.Begin);

                var cd = new ContentDisposition
                {
                    FileName = HttpUtility.UrlEncode(originalFileName),
                    Inline = false
                };
                Response.Headers.Add("Content-Disposition", cd.ToString());
                Response.Headers.Add("X-Content-Type-Options", "nosniff");

                return File(toReturn, "text/plain");
            });
        }

        #endregion Logs

    }
}