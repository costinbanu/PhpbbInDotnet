using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PhpbbInDotnet.Utilities;
using Serilog;
using System;

namespace PhpbbInDotnet.Forum.Pages.PhpbbRedirects
{
    public class fileModel : PageModel
    {
        private readonly ILogger _logger;

        public fileModel(ILogger logger)
        {
            _logger = logger;
        }

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

            _logger.Warning("Bad request to legacy file.php route: {route}", Request.QueryString.Value);
            return BadRequest();
        }
    }
}