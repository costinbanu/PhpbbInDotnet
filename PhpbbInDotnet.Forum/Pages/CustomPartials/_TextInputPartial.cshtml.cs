using Microsoft.AspNetCore.Mvc.RazorPages;

namespace PhpbbInDotnet.Forum.Pages.CustomPartials
{
    public class _TextInputPartialModel : PageModel
    {
        public string Language { get; }

        public _TextInputPartialModel(string language)
        {
            Language = language;
        }
    }
}
