using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Configuration;
using Serverless.Forum.Contracts;
using Serverless.Forum.ForumDb;
using Serverless.Forum.Services;
using Serverless.Forum.Utilities;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Serverless.Forum.Pages
{
    public partial class AdminModel : ModelWithLoggedUser
    {
        public AdminCategories Category { get; private set; } = AdminCategories.Users;
        public bool? IsSuccess { get; private set; }
        public string Message { get; private set; }
        public string MessageClass
        {
            get
            {
                switch (IsSuccess)
                {
                    case null:
                        return "message";
                    case true:
                        return "message success";
                    case false:
                    default:
                        return "message fail";
                }
            }
        }

        private readonly AdminUserService _adminUserService;
        private readonly AdminForumService _adminForumService;
        private readonly AdminWritingToolsService _adminWritingService;

        public AdminModel (
            IConfiguration config, Utils utils, ForumTreeService forumService, UserService userService, CacheService cacheService, 
            AdminUserService adminUserService, AdminForumService adminForumService, AdminWritingToolsService adminWritingService
        ) : base(config, utils, forumService, userService, cacheService)
        {
            _adminUserService = adminUserService;
            _adminForumService = adminForumService;
            _adminWritingService = adminWritingService;
            UserSearchResults = new List<PhpbbUsers>();
            ForumChildren = new List<PhpbbForums>();
            ForumSelectedParent = new List<SelectListItem>();
        }

        public async Task<IActionResult> OnGet()
        {
            var validationResult = await ValidatePermissionsAndInit(AdminCategories.Users);
            if (validationResult != null)
            {
                return validationResult;
            }

            return Page();
        }

        private async Task<IActionResult> ValidatePermissionsAndInit(AdminCategories category)
            => !await IsCurrentUserAdminHereAsync() ? Forbid() : null;

        #region Admin user

        public List<PhpbbUsers> UserSearchResults { get; private set; }

        public async Task<IActionResult> OnPostUserSearch(string username, string email, int? userid)
        {
            var validationResult = await ValidatePermissionsAndInit(AdminCategories.Users);
            if (validationResult != null)
            {
                return validationResult;
            }

            UserSearchResults = await _adminUserService.UserSearchAsync(username, email, userid);
            Category = AdminCategories.Users;
            if (!UserSearchResults.Any())
            {
                Message = $"Nu a fost găsit nici un utilizator cu username-ul '{username}'.";
                IsSuccess = false;
            }
            return Page();
        }

        public async Task<IActionResult> OnPostUserManagement(AdminUserActions? userAction, int? userId)
        {
            var validationResult = await ValidatePermissionsAndInit(AdminCategories.Users);
            if (validationResult != null)
            {
                return validationResult;
            }

            (Message, IsSuccess) = await _adminUserService.ManageUserAsync(userAction, userId);
            Category = AdminCategories.Users;
            return Page();
        }

        #endregion Admin user

        #region Admin forum

        [BindProperty]
        public PhpbbForums Forum { get; set; } = null;
        public List<PhpbbForums> ForumChildren { get; private set; }
        public List<SelectListItem> ForumSelectedParent { get; private set; }
        public IEnumerable<ForumPermissions> Permissions { get; private set; }
        public bool ShowForum { get; private set; }

        public async Task<IActionResult> OnPostShowForum(int? forumId)
        {
            var validationResult = await ValidatePermissionsAndInit(AdminCategories.Users);
            if (validationResult != null)
            {
                return validationResult;
            }

            if (forumId != null)
            {
                Permissions = await _adminForumService.GetPermissions(forumId.Value);
                (Forum, ForumChildren) = await _adminForumService.ShowForum(forumId.Value);
            }

            ShowForum = true;
            Category = AdminCategories.Forums;
            return Page();
        }

        public async Task<IActionResult> OnPostForumManagement(
            int? forumId, string forumName, string forumDesc, bool? hasPassword, string forumPassword, int? parentId, 
            ForumType? forumType, List<int> childrenForums, List<string> userForumPermissions, List<string> groupForumPermissions
        ) 
        {
            var validationResult = await ValidatePermissionsAndInit(AdminCategories.Users);
            if (validationResult != null)
            {
                return validationResult;
            }

            (Message, IsSuccess) = await _adminForumService.ManageForumsAsync(
                forumId, forumName, forumDesc, hasPassword, forumPassword, parentId, forumType, childrenForums, userForumPermissions, groupForumPermissions
            );

            ShowForum = false;
            Category = AdminCategories.Forums;
            return Page();
        }

        #endregion Admin forum

        #region Admin writing

        public async Task<IActionResult> OnPostBanWords(List<PhpbbWords> words, List<int> toRemove)
        {
            var validationResult = await ValidatePermissionsAndInit(AdminCategories.Users);
            if (validationResult != null)
            {
                return validationResult;
            }

            (Message, IsSuccess) = await _adminWritingService.ManageBannedWords(words, toRemove);

            Category = AdminCategories.WritingTools;

            return Page();
        }

        #endregion Admin writing

    }
}