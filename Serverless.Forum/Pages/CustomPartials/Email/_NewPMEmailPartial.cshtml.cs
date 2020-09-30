using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Serverless.Forum.Pages.CustomPartials.Email
{
    public class _NewPMEmailPartialModel : PageModel
    {
        public string SenderName { get; set; }
    }
}