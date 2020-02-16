using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Serverless.Forum.ForumDb;
using Serverless.Forum.Pages.CustomPartials.Admin;
using Serverless.Forum.Utilities;
using System.Collections.Generic;
using System.Threading.Tasks;
using System;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Serverless.Forum.Services;
using Serverless.Forum.Contracts;
using Microsoft.AspNetCore.Mvc.Rendering;

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

        public AdminModel(IConfiguration config, Utils utils, ForumTreeService forumService, UserService userService, CacheService cacheService, AdminUserService adminUserService, AdminForumService adminForumService)
            : base(config, utils, forumService, userService, cacheService)
        {
            _adminUserService = adminUserService;
            _adminForumService = adminForumService;
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

        public PhpbbForums Forum { get; set; } = null;
        public int SelectedForumId { get; private set; }
        public List<PhpbbForums> ForumChildren { get; private set; }
        public List<SelectListItem> ForumSelectedParent { get; private set; }
        [BindProperty]
        public int ParentId { get; private set; }
        public IEnumerable<ForumPermissions> Permissions { get; private set; }

        public async Task<IActionResult> OnPostShowForum(int forumId)
        {
            var validationResult = await ValidatePermissionsAndInit(AdminCategories.Users);
            if (validationResult != null)
            {
                return validationResult;
            }

            Permissions = await _adminForumService.GetPermissions(forumId);
            (Forum, ForumChildren) = await _adminForumService.ShowForum(forumId);
            SelectedForumId = forumId;
            ParentId = Forum.ParentId;

            Category = AdminCategories.Forums;
            return Page();
        }

        public async Task<IActionResult> OnPostForumManagement(List<int> childrenForums, int forumId, string forumName, string forumDesc, int parentId)
        {
            var validationResult = await ValidatePermissionsAndInit(AdminCategories.Users);
            if (validationResult != null)
            {
                return validationResult;
            }

            (Message, IsSuccess) = await _adminForumService.ManageForumsAsync(childrenForums, forumId, forumName, forumDesc);
            Category = AdminCategories.Forums;
            return Page();
        }

        #endregion Admin forum

    }
}