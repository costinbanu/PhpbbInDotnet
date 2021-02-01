﻿using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Configuration;
using PhpbbInDotnet.Objects;
using PhpbbInDotnet.Database;
using PhpbbInDotnet.Database.Entities;
using PhpbbInDotnet.Forum.Pages.CustomPartials.Admin;
using PhpbbInDotnet.Services;
using PhpbbInDotnet.Utilities;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using PhpbbInDotnet.Languages;

namespace PhpbbInDotnet.Forum.Pages
{
    [RequestSizeLimit(10 * 1024 * 1024)]
    public partial class AdminModel : AuthenticatedPageModel
    {
        public AdminCategories Category { get; private set; } = AdminCategories.Users;
        public bool? IsSuccess { get; private set; }
        public string Message { get; private set; }
        public string MessageClass
            => IsSuccess switch
            {
                null => "message",
                true when string.IsNullOrWhiteSpace(Message) => "message",
                true => "message success",
                _ => "message fail",
            };

        private readonly AdminUserService _adminUserService;
        private readonly AdminForumService _adminForumService;
        private readonly WritingToolsService _adminWritingService;

        public AdminModel(ForumDbContext context, ForumTreeService forumService, UserService userService, CacheService cacheService, CommonUtils utils, 
            AdminUserService adminUserService, AdminForumService adminForumService, WritingToolsService adminWritingService, IConfiguration config, 
            AnonymousSessionCounter sessionCounter, LanguageProvider languageProvider) 
            : base(context, forumService, userService, cacheService, config, sessionCounter, utils, languageProvider)
        {
            _adminUserService = adminUserService;
            _adminForumService = adminForumService;
            _adminWritingService = adminWritingService;
            UserSearchResults = new List<PhpbbUsers>();
            ForumChildren = new List<PhpbbForums>();
            ForumSelectedParent = new List<SelectListItem>();
        }

        public async Task<IActionResult> OnGet()
            => await WithAdmin(async () => await Task.FromResult(Page()));

        #region Admin user

        [BindProperty]
        public AdminUserSearch SearchParameters { get; set; }
        public List<PhpbbUsers> UserSearchResults { get; private set; }

        public async Task<IActionResult> OnPostUserSearch()
            => await WithAdmin(async () =>
            {
                Category = AdminCategories.Users;
                if (new[] { SearchParameters?.Username, SearchParameters?.Email, SearchParameters?.RegisteredFrom, SearchParameters?.RegisteredTo, SearchParameters?.LastActiveFrom, SearchParameters?.LastActiveTo }.All(string.IsNullOrWhiteSpace) 
                    && ((SearchParameters?.UserId ?? 0) == 0) 
                    && !(SearchParameters?.NeverActive ?? false))
                {
                    Message = "Prea puține criterii de căutare.";
                    IsSuccess = false;
                    return Page();
                }

                (IsSuccess, Message, UserSearchResults) = await _adminUserService.UserSearchAsync(SearchParameters);
                if (!UserSearchResults.Any())
                {
                    Message = "Nu a fost găsit nici un utilizator.";
                    IsSuccess = false;
                }
                return Page();
            });

        public async Task<IActionResult> OnPostUserManagement(AdminUserActions? userAction, int? userId)
            => await WithAdmin(async () =>
            {
                (Message, IsSuccess) = await _adminUserService.ManageUser(userAction, userId);
                Category = AdminCategories.Users;
                return Page();
            });

        public async Task<IActionResult> OnPostBatchUserManagement(int[] userIds)
            => await WithAdmin(async () =>
            {
                (Message, IsSuccess) = await _adminUserService.DeleteUsersWithEmailNotConfirmed(userIds);
                Category = AdminCategories.Users;
                return Page();
            });

        public async Task<IActionResult> OnPostGroupManagement(UpsertGroupDto dto)
            => await WithAdmin(async () =>
            {
                (Message, IsSuccess) = await _adminUserService.ManageGroup(dto);
                Category = AdminCategories.Users;
                return Page();
            });

        public async Task<IActionResult> OnPostRankManagement(int? rankId, string rankName, bool? deleteRank)
            => await WithAdmin(async () =>
            {
                (Message, IsSuccess) = await _adminUserService.ManageRank(rankId, rankName, deleteRank);
                Category = AdminCategories.Users;
                return Page();
            });

        public async Task<IActionResult> OnPostBanUser(List<PhpbbBanlist> banlist, List<int> toRemove)
            => await WithAdmin(async () =>
            {
                (Message, IsSuccess) = await _adminUserService.BanUser(banlist, toRemove);
                Category = AdminCategories.Users;
                return Page();
            });

        #endregion Admin user

        #region Admin forum

        [BindProperty]
        public PhpbbForums Forum { get; set; } = null;
        [BindProperty]
        public int? ParentForumId { get; set; } = null;
        public List<PhpbbForums> ForumChildren { get; private set; }
        public List<SelectListItem> ForumSelectedParent { get; private set; }
        public IEnumerable<ForumPermissions> Permissions { get; private set; }
        public bool ShowForum { get; private set; }

        public async Task<IActionResult> OnPostShowForum(int? forumId)
            => await WithAdmin(async () =>
            {
                if (forumId != null)
                {
                    Permissions = await _adminForumService.GetPermissions(forumId.Value);
                    (Forum, ForumChildren) = await _adminForumService.ShowForum(forumId.Value);
                }

                ShowForum = true;
                Category = AdminCategories.Forums;
                return Page();
            });

        public async Task<IActionResult> OnPostForumManagement(UpsertForumDto dto)
            => await WithAdmin(async () =>
            {
                (Message, IsSuccess) = await _adminForumService.ManageForumsAsync(dto);

                ShowForum = false;
                Category = AdminCategories.Forums;
                return Page();
            });

        public async Task<IActionResult> OnPostDeleteForum(int forumId)
            => await WithAdmin(async () =>
            {
                (Message, IsSuccess) = await _adminForumService.DeleteForum(forumId);

                ShowForum = false;
                Category = AdminCategories.Forums;
                return Page();
            });

        #endregion Admin forum

        #region Admin writing

        public async Task<IActionResult> OnPostBanWords(List<PhpbbWords> words, List<int> toRemove)
            => await WithAdmin(async () =>
            {
                (Message, IsSuccess) = await _adminWritingService.ManageBannedWords(words, toRemove);
                Category = AdminCategories.WritingTools;
                return Page();
            });

        public async Task<IActionResult> OnPostOrphanedFiles()
            => await WithAdmin(async () =>
            {
                (Message, IsSuccess) = await _adminWritingService.DeleteOrphanedFiles();

                Category = AdminCategories.WritingTools;

                return Page();
            });

        public async Task<IActionResult> OnPostBBCodes(List<PhpbbBbcodes> codes, List<int> toRemove, List<int> toDisplay)
            => await WithAdmin(async () =>
            {
                (Message, IsSuccess) = await _adminWritingService.ManageBBCodes(codes, toRemove, toDisplay);
                Category = AdminCategories.WritingTools;
                return Page();
            });

        #endregion Admin writing

    }
}