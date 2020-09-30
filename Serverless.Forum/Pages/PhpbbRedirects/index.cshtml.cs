using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Serverless.Forum.Pages.PhpbbRedirects
{
    public class indexModel : PageModel
    {
        public IActionResult OnGet()
        {
            return RedirectToPage("Index");
        }
    }
}