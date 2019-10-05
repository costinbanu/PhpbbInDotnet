using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Serverless.Forum.Pages
{
    public class _AttachmentPartialModel : PageModel 
    {
        public bool IsRenderedInline = false;
        public bool IsDisplayedInline = false;
        public int Id;
        public string FileName;
        public string MimeType;

        public void OnGet()
        {
        }
    }
}