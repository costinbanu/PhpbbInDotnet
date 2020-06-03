using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Serverless.Forum.Pages
{
    public class ErrorModel : PageModel
    {
        public string RequestId { get; set; }

        public void OnGet(string id)
        {
            RequestId = id;
        }
    }
}
