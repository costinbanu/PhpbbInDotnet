using Microsoft.AspNetCore.Mvc.RazorPages;

namespace PhpbbInDotnet.Forum.Pages.CustomPartials.Admin
{
    public class _AdminWritingModel : PageModel
    {
        public int CurrentUserId { get; }

        public _AdminWritingModel(int currentUserId)
        {
            CurrentUserId = currentUserId;
        }
    }
}