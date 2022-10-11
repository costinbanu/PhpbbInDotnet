using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PhpbbInDotnet.Domain.Utilities;

namespace PhpbbInDotnet.Forum.Pages.PhpbbRedirects
{
    public class fileModel : PageModel
    {
        public IActionResult OnGet(int? id, string avatar)
        {
            if (id.HasValue)
            {
                var redirect = ForumLinkUtility.GetRedirectObjectToFile(fileId: id.Value);
                return RedirectToPage(redirect.Url, redirect.RouteValues);
            }

            if (!string.IsNullOrWhiteSpace(avatar) && int.TryParse(avatar.Split('_')[0], out var userId))
            {
                var redirect = ForumLinkUtility.GetRedirectObjectToAvatar(userId);
                return RedirectToPage(redirect.Url, redirect.RouteValues);
            }

            return BadRequest();
        }
    }
}