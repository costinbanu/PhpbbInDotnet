using Microsoft.AspNetCore.Mvc.RazorPages;

namespace PhpbbInDotnet.Forum.Pages.CustomPartials
{
    public class _HeaderLinksPartialModel : PageModel
    {
        public _HeaderLinksPartialModel(bool shiftRight = false, params string[] extraElements)
        {
            ExtraElements = extraElements;
            ShiftRight = shiftRight;
        }

        public string[] ExtraElements { get; }
        public bool ShiftRight { get; }
    }
}