using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace PhpbbInDotnet.Forum.Pages
{
    public class ErrorModel : PageModel
    {
        [BindProperty(SupportsGet = true)]
        public string? ErrorId { get; set; }

        [BindProperty(SupportsGet = true)]
        public bool IsUnauthorized { get; set; }

        [BindProperty(SupportsGet = true)]
        public bool IsNotFound { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? CustomErrorMessage { get; set; }

        public void OnGet()
        {

        }
    }
}
