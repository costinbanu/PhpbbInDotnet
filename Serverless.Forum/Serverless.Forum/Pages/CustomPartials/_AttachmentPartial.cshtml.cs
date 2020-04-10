using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Serverless.Forum.Pages.CustomPartials
{
    public class _AttachmentPartialModel : PageModel 
    {
        public bool IsRenderedInline { get; set; } = false;
        public int Id { get; set; }
        public string FileName { get; set; }
        public string MimeType { get; set; }
        public int DownloadCount { get; set; }
        public string Comment { get; set; }
        public long FileSize { get; set; }
    }
}