using Microsoft.AspNetCore.Mvc.RazorPages;
using PhpbbInDotnet.Utilities;

namespace PhpbbInDotnet.Forum.Pages.CustomPartials.Admin
{
    public class _AdminViewLogsModel : PageModel
    {
        public OperationLogType? LogType { get; set; }
        
        public int LogPage { get; set; } = 1;

        public string AuthorName { get; set; }

        public string Language { get; set; }

        public string DateFormat { get; set; }
    }
}
