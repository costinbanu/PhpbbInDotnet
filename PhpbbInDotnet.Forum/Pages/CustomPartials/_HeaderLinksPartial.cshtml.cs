using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Collections.Generic;

namespace PhpbbInDotnet.Forum.Pages.CustomPartials
{
    public class _HeaderLinksPartialModel : PageModel
    {
        public _HeaderLinksPartialModel(string language, params string[] extraElements)
        {
            ExtraElements = extraElements;
            Language = language;
        }

        public _HeaderLinksPartialModel(string language, IEnumerable<string> extraElements)
        {
            ExtraElements = extraElements;
            Language = language;
        }

        public IEnumerable<string> ExtraElements { get; }
        public string Language { get; }
    }
}