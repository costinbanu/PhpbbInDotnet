using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System;

namespace PhpbbInDotnet.Forum.Pages.PhpbbRedirects
{
    public class memberlistModel : PageModel
    {
        public IActionResult OnGet(string? mode, int? u, int? g)
        {
            if (mode?.Equals("viewprofile", StringComparison.InvariantCultureIgnoreCase) == true && u > 0)
            {
                return RedirectToPage(
                    "../User", 
                    new 
                    { 
                        UserId = u 
                    });
            }
            else if (mode?.Equals("group", StringComparison.InvariantCultureIgnoreCase) == true && g > 0)
            {
                return RedirectToPage(
                    "../MemberList",
                    new
                    {
                        GroupId = g,
                        Mode = "SearchUsers",
                        handler = "search"
                    });
            }
            return RedirectToPage("../MemberList");
        }
    }
}
