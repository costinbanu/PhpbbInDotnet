using Microsoft.AspNetCore.Mvc.RazorPages;
using Serverless.Forum.ForumDb.Entities;
using System.Collections.Generic;

namespace Serverless.Forum.Pages.CustomPartials.Admin
{
    public class _AdminUsersPartialModel : PageModel
    {
        public string DateFormat { get; private set; }
        public List<PhpbbUsers> SearchResults { get; private set; }

        public _AdminUsersPartialModel(string dateFormat, List<PhpbbUsers> searchResults)
        {
            DateFormat = dateFormat;
            SearchResults = searchResults;
        }
    }
}