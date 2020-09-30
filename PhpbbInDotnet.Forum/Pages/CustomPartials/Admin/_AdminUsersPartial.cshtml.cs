using Microsoft.AspNetCore.Mvc.RazorPages;
using PhpbbInDotnet.Database.Entities;
using System.Collections.Generic;

namespace PhpbbInDotnet.Forum.Pages.CustomPartials.Admin
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