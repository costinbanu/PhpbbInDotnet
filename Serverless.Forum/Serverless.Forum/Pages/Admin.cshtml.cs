using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Serverless.Forum.Utilities;
using System.Threading.Tasks;

namespace Serverless.Forum.Pages
{
    public class AdminModel : ModelWithLoggedUser
    {
        public IConfiguration Config => _config;
        public Utils Utils => _utils;

        public AdminModel(IConfiguration config, Utils utils) : base(config, utils)
        {
        }

        public async Task<IActionResult> OnGet()
        {
            if(!await IsCurrentUserAdminHereAsync())
            {
                return Forbid();
            }

            return Page();
        }
    }
}