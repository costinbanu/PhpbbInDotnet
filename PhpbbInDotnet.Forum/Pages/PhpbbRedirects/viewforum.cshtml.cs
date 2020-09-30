using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace PhpbbInDotnet.Forum.Pages.PhpbbRedirects
{
    public class viewforumModel : PageModel
    {
        public IActionResult OnGet(int f)
        {
            return RedirectToPage("../ViewForum", new { forumId = f });
        }
    }
}