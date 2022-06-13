using Microsoft.AspNetCore.Mvc.RazorPages;

namespace PhpbbInDotnet.Forum.Pages.CustomPartials
{
    public class _TextInputPartialModel : PageModel
    {
        public string Language { get; }
        public string? PostTitle { get; }
        public string? PostText { get; }

        public _TextInputPartialModel(string language, string? postTitle, string? postText)
        {
            Language = language;
            PostTitle = postTitle;
            PostText = postText;
        }
    }
}
