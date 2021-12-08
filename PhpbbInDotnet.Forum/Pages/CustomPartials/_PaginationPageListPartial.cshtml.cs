using Microsoft.AspNetCore.Mvc.RazorPages;
using PhpbbInDotnet.Objects;
using PhpbbInDotnet.Utilities;

namespace PhpbbInDotnet.Forum.Pages.CustomPartials
{
    public class _PaginationPageListPartialModel : PageModel
    {
        public PaginationDto? Pagination { get; set; }

        public string Language { get; set; } = Constants.DEFAULT_LANGUAGE;
    }
}
