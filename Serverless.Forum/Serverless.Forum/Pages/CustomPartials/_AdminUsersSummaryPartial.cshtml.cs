using Microsoft.AspNetCore.Mvc.RazorPages;
using Serverless.Forum.ForumDb;
using System.Collections.Generic;

namespace Serverless.Forum.Pages.CustomPartials.Admin
{
    public class _AdminUsersSummaryPartialModel : PageModel
    {
        public string DateFormat { get; private set; }
        public IEnumerable<PhpbbUsers> Users { get; private set; }

        public _AdminUsersSummaryPartialModel(string dateFormat, IEnumerable<PhpbbUsers> users)
        {
            DateFormat = dateFormat;
            Users = users;
        }
    }
}