using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Serverless.Forum.Contracts;
using Serverless.Forum.ForumDb;
using Serverless.Forum.Pages.CustomPartials.Admin;
using Serverless.Forum.Services;
using Serverless.Forum.Utilities;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Serverless.Forum.Pages
{
    [RequestSizeLimit(10 * 1024 * 1024)]
    public partial class AdminModel : ModelWithLoggedUser
    {
        public AdminCategories Category { get; private set; } = AdminCategories.Users;
        public bool? IsSuccess { get; private set; }
        public string Message { get; private set; }
        public string MessageClass
            => IsSuccess switch
            {
                null => "message",
                true => "message success",
                _ => "message fail",
            };

        private readonly Utils _utils;
        private readonly AdminUserService _adminUserService;
        private readonly AdminForumService _adminForumService;
        private readonly WritingToolsService _adminWritingService;

        public AdminModel(ForumDbContext context, ForumTreeService forumService, UserService userService, CacheService cacheService,
            Utils utils, AdminUserService adminUserService, AdminForumService adminForumService, WritingToolsService adminWritingService) 
            : base(context, forumService, userService, cacheService)
        {
            _utils = utils;
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

        public async Task<IActionResult> OnPostForumManagement(UpsertForumDto dto)
        {
            var validationResult = await ValidatePermissionsAndInit(AdminCategories.Users);
            if (validationResult != null)
            {
                return validationResult;
            }

            (Message, IsSuccess) = await _adminForumService.ManageForumsAsync(dto);

            ShowForum = false;
            Category = AdminCategories.Forums;
            return Page();
        }

        #endregion Admin forum

        #region Admin writing

        public async Task<IActionResult> OnGetWriting()
        {
            var validationResult = await ValidatePermissionsAndInit(AdminCategories.Users);
            if (validationResult != null)
            {
                return validationResult;
            }

            var result = await _utils.RenderRazorViewToString("_AdminWriting", new _AdminWritingModel(CurrentUserId), PageContext, HttpContext);
            return Content(result);
        }

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

        public async Task<IActionResult> OnPostOrphanedFiles(AdminOrphanedFilesActions action)
        {
            var validationResult = await ValidatePermissionsAndInit(AdminCategories.Users);
            if (validationResult != null)
            {
                return validationResult;
            }

            var (inS3, inDb) = await _cacheService.GetFromCache<(IEnumerable<string> inS3, IEnumerable<int> inDb)>(_adminWritingService.GetCacheKey(CurrentUserId));
            if (action == AdminOrphanedFilesActions.DeleteFromDb)
            {
                (Message, IsSuccess) = await _adminWritingService.DeleteDbOrphanedFiles(inDb);
            }
            else if (action == AdminOrphanedFilesActions.DeleteFromS3)
            {
                (Message, IsSuccess) = await _adminWritingService.DeleteS3OrphanedFiles(inS3);
            }

            if(IsSuccess ?? false)
            {
                await _cacheService.RemoveFromCache(_adminWritingService.GetCacheKey(CurrentUserId));
            }

            Category = AdminCategories.WritingTools;

            return Page();
        }

        #endregion Admin writing

    }
}