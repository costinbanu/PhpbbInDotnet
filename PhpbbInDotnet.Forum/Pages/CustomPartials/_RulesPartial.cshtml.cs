using Microsoft.AspNetCore.Mvc.RazorPages;
using PhpbbInDotnet.Utilities;

namespace PhpbbInDotnet.Forum.Pages.CustomPartials
{
    public class _RulesPartialModel : PageModel
    {
        public string? ForumRules { get; set; }
        public string? ForumRulesLink { get; set; }
        public string Language { get; set; } = Constants.DEFAULT_LANGUAGE;
    }
}