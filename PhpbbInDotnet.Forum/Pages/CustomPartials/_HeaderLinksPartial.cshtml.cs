using Microsoft.AspNetCore.Mvc.RazorPages;

namespace PhpbbInDotnet.Forum.Pages.CustomPartials
{
    public class _HeaderLinksPartialModel : PageModel
    {
        public _HeaderLinksPartialModel(bool isIndex = false, params string[] extraElements)
        {
            ExtraElements = extraElements;
            IsIndex = isIndex;
        }

        public string[] ExtraElements { get; }
        public bool IsIndex { get; }
    }
}