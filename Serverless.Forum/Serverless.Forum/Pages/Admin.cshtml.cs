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
using Serverless.Forum.Admin;
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

        private readonly UserService _userService;
        private readonly ForumService _forumService;

        public AdminModel(IConfiguration config, Utils utils, UserService userService, ForumService forumService) : base(config, utils)
        {
            _userService = userService;
            _forumService = forumService;
            UserSearchResults = new List<PhpbbUsers>();
            ForumChildren = new List<PhpbbForums>();
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

            UserSearchResults = await _userService.UserSearchAsync(username, email, userid);
            Category = AdminCategories.Users;
            return Page();
        }

        public async Task<IActionResult> OnPostUserManagement(AdminUserActions? userAction, int? userId)
        {
            var validationResult = await ValidatePermissionsAndInit(AdminCategories.Users);
            if (validationResult != null)
            {
                return validationResult;
            }

            (Message, IsSuccess) = await _userService.ManageUserAsync(userAction, userId);
            Category = AdminCategories.Users;
            return Page();
        }

        #endregion Admin user

        #region Admin forum

        public PhpbbForums Forum { get; set; } = null;
        public int SelectedForumId { get; private set; }
        public List<PhpbbForums> ForumChildren { get; private set; }
        //public List<SelectListItem> ForumSelectedParent { get; private set; }

        public async Task<IActionResult> OnPostShowForum(int forumId)
        {
            var validationResult = await ValidatePermissionsAndInit(AdminCategories.Users);
            if (validationResult != null)
            {
                return validationResult;
            }

            (Forum, ForumChildren) = await _forumService.ShowForum(forumId);
            SelectedForumId = forumId;

            Category = AdminCategories.Forums;
            return Page();
        }

        public async Task<IActionResult> OnPostForumManagement(List<int> childrenForums, int forumId, string forumName, string forumDesc)
        {
            var validationResult = await ValidatePermissionsAndInit(AdminCategories.Users);
            if (validationResult != null)
            {
                return validationResult;
            }

            (Message, IsSuccess) = await _forumService.ManageForumsAsync(childrenForums, forumId, forumName, forumDesc);
            Category = AdminCategories.Forums;
            return Page();
        }

        #endregion Admin forum

    }
}