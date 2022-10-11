using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Collections.Generic;

namespace PhpbbInDotnet.Forum.Pages.CustomPartials
{
    public class _HeaderLinksPartialModel : PageModel
    {
        public _HeaderLinksPartialModel(string language, bool isAnonymous)
        {
            ExtraElements = new List<string>();
            Language = language;
            IsAnonymous = isAnonymous;
        }

        public _HeaderLinksPartialModel(string language, bool isAnonymous, IEnumerable<string> extraElements)
        {
            ExtraElements = extraElements;
            Language = language;
            IsAnonymous = isAnonymous;
        }

        public IEnumerable<string> ExtraElements { get; }
        public string Language { get; }
        public bool IsAnonymous { get; }
    }
}