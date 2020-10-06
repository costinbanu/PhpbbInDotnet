using Microsoft.AspNetCore.Mvc.RazorPages;

namespace PhpbbInDotnet.Forum.Pages.CustomPartials
{
    public class _HeaderLinksPartialModel : PageModel
    {
        public _HeaderLinksPartialModel(params string[] extraElements)
        {
            ExtraElements = extraElements;
        }

        public string[] ExtraElements { get; }
    }
}