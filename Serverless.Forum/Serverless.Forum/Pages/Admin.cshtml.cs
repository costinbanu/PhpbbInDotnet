using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Serverless.Forum.ForumDb;
using Serverless.Forum.Pages.CustomPartials.Admin;
using Serverless.Forum.Utilities;
using System.Threading.Tasks;

namespace Serverless.Forum.Pages
{
    public partial class AdminModel : ModelWithLoggedUser
    {
        public AdminCategories Category { get; private set; } = AdminCategories.Users;
        public _AdminUsersPartialModel AdminUsers { get; private set; }

        private _AdminForumsPartialModel _adminForums;

        public AdminModel(IConfiguration config, Utils utils) : base(config, utils)
        {
            AdminUsers = new _AdminUsersPartialModel(config, utils);
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

        #region Admin user

        public async Task<IActionResult> OnPostUserManagement(AdminUserActions? action, int? id)
        {
            var validationResult = await ValidatePermissionsAndInit(AdminCategories.Users);
            if (validationResult != null)
            {
                return validationResult;
            }

            await AdminUsers.ManageUserAsync(action, id);
            return Page();
        }

        public async Task<IActionResult> OnPostUserSearch(string username, string email, int? userid)
        {
            var validationResult = await ValidatePermissionsAndInit(AdminCategories.Users);
            if (validationResult != null)
            {
                return validationResult;
            }

            await AdminUsers.UserSearchAsync(username, email, userid);
            Category = AdminCategories.Users;
            return Page();
        }

        #endregion Admin user

        #region Admin forum

        public async Task<IActionResult> OnPostForumManagement(int forumId, int[] childrenForums, PhpbbForums changedForum)
        {
            var validationResult = await ValidatePermissionsAndInit(AdminCategories.Users);
            if (validationResult != null)
            {
                return validationResult;
            }

            await (await GetAdminForumsModelLazy()).ManageForumsAsync(forumId, childrenForums, changedForum);
            Category = AdminCategories.Forums;
            return Page();
        }

        public async Task<IActionResult> OnPostShowForum(int forumId)
        {
            var validationResult = await ValidatePermissionsAndInit(AdminCategories.Users);
            if (validationResult != null)
            {
                return validationResult;
            }

            await (await GetAdminForumsModelLazy()).ShowForum(forumId, async (id) => await PathToForumOrTopic(id, null));
            Category = AdminCategories.Forums;
            return Page();
        }

        public async Task<_AdminForumsPartialModel> GetAdminForumsModelLazy()
            => _adminForums ?? (_adminForums = new _AdminForumsPartialModel(_config, _utils, await GetForumTree(), await PathToForumOrTopic(0, null)));

        #endregion Admin forum

        private async Task<IActionResult> ValidatePermissionsAndInit(AdminCategories category)
        {
            if (!await IsCurrentUserAdminHereAsync())
            {
                return Forbid();
            }

            AdminUsers.DateFormat = (await GetCurrentUserAsync()).UserDateFormat;
            Category = AdminCategories.Users;
            return null;
        }
    }
}