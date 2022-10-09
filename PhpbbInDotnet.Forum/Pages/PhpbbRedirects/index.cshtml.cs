using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PhpbbInDotnet.Domain.Utilities;

namespace PhpbbInDotnet.Forum.Pages.PhpbbRedirects
{
    public class indexModel : PageModel
    {
        public IActionResult OnGet()
        {
            return RedirectToPage(ForumLinkUtility.IndexPage);
        }
    }
}