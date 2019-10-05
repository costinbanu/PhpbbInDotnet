using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Serverless.Forum.Pages
{
    public class __PhpbbRedirects_viewforumModel : PageModel
    {
        public IActionResult OnGet(int f)
        {
            return RedirectToPage("ViewForum", new { forumId = f });
        }
    }
}