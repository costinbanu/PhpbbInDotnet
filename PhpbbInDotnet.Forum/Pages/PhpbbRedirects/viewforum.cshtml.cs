using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PhpbbInDotnet.Domain.Utilities;

namespace PhpbbInDotnet.Forum.Pages.PhpbbRedirects
{
    public class viewforumModel : PageModel
    {
        public IActionResult OnGet(int f)
        {
            var redirect = ForumLinkUtility.GetRedirectObjectToForum(forumId: f);
            return RedirectToPage(redirect.Url, redirect.RouteValues);
        }
    }
}