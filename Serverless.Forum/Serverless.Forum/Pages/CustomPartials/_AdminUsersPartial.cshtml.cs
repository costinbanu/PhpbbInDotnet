using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Serverless.Forum.ForumDb;
using Serverless.Forum.Utilities;

namespace Serverless.Forum.Pages.CustomPartials
{
    public class _AdminUsersPartialModel : PageModel
    {
        private readonly IConfiguration _config;
        private readonly Utils _utils;

        public List<PhpbbUsers> InactiveUsers { get; private set; }
        public string UserDateFormat { get; private set; }

        public _AdminUsersPartialModel(IConfiguration config, Utils utils, string dateFormat) 
        {
            _config = config;
            _utils = utils;
            UserDateFormat = dateFormat;
        }

        public async Task Init()
        {
            using (var context = new ForumDbContext(_config))
            {
                InactiveUsers = await (
                    from u in context.PhpbbUsers
                    where u.UserInactiveTime > 0
                       && u.UserInactiveReason != UserInactiveReason.NotInactive
                    select u
                ).ToListAsync();
            }
        }
    }
}