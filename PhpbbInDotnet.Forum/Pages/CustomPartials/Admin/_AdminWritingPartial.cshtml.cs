using Microsoft.AspNetCore.Mvc.RazorPages;

namespace PhpbbInDotnet.Forum.Pages.CustomPartials.Admin
{
    public class _AdminWritingPartialModel : PageModel
    {
        public string DateFormat { get; set; }

        public string Language { get; set; }
    }
}