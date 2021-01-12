using Microsoft.AspNetCore.Mvc.RazorPages;

namespace PhpbbInDotnet.Forum.Pages.CustomPartials
{
    public class _HeaderLinksPartialModel : PageModel
    {
        public _HeaderLinksPartialModel(string language, bool isIndex = false, params string[] extraElements)
        {
            ExtraElements = extraElements;
            IsIndex = isIndex;
            Language = language;
        }

        public string[] ExtraElements { get; }
        public bool IsIndex { get; }
        public string Language { get; }
    }
}