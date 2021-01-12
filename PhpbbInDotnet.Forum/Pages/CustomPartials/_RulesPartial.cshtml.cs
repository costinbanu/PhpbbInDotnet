using Microsoft.AspNetCore.Mvc.RazorPages;

namespace PhpbbInDotnet.Forum.Pages.CustomPartials
{
    public class _RulesPartialModel : PageModel
    {
        public string ForumRules { get; set; }
        public string ForumRulesLink { get; set; }
        public string Language { get; set; }
    }
}