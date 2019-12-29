using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System;

namespace Serverless.Forum.Pages.PhpbbRedirects
{
    public class fileModel : PageModel
    {
        public IActionResult OnGet(int? id, int? avatar)
        {
            if (id.HasValue)
            {
                return RedirectToPage("../File", new { Id = id.Value });
            }
            else if (avatar.HasValue)
            {
                throw new NotImplementedException();
            }
            else
            {
                return BadRequest();
            }
        }
    }
}