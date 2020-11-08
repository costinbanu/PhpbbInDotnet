using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace PhpbbInDotnet.Forum.Pages.PhpbbRedirects
{
    public class indexModel : PageModel
    {
        public IActionResult OnGet()
        {
            return RedirectToPage("../Index");
        }
    }
}