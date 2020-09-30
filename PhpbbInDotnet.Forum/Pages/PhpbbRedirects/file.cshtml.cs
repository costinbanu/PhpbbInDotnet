using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace PhpbbInDotnet.Forum.Pages.PhpbbRedirects
{
    public class fileModel : PageModel
    {
        public IActionResult OnGet(int? id, string avatar)
        {
            if (id.HasValue)
            {
                return RedirectToPage("../File", new { Id = id.Value });
            }

            if (!string.IsNullOrWhiteSpace(avatar) && int.TryParse(avatar.Split('_')[0], out var userId))
            {
                return RedirectToPage("../File", "avatar",  new { userId });
            }

            return BadRequest();
        }
    }
}