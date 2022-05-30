using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PhpbbInDotnet.Utilities;
using System;

namespace PhpbbInDotnet.Forum.Pages.PhpbbRedirects
{
    public class fileModel : PageModel
    {
        private readonly ICommonUtils _utils;

        public fileModel(ICommonUtils utils)
        {
            _utils = utils;
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

            _utils.HandleErrorAsWarning(new Exception($"Bad request to legacy file.php route: {Request.QueryString.Value}"));
            return RedirectToPage("../Index");
        }
    }
}