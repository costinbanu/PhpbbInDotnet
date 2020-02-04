using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Serverless.Forum.Pages.CustomPartials.Admin;
using Serverless.Forum.Utilities;
using System.Threading.Tasks;

namespace Serverless.Forum.Pages
{
    public partial class AdminModel : ModelWithLoggedUser
    {
        //public IConfiguration Config => _config;
        //public Utils Utils => _utils;
        public AdminCategories Category { get; private set; } = AdminCategories.Users;
        public _AdminUsersPartialModel AdminUsers { get; private set; }

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
            return Page();
        }

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