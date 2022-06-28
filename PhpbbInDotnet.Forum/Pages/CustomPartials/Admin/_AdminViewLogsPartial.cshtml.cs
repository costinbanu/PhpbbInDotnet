using Microsoft.AspNetCore.Mvc.RazorPages;
using PhpbbInDotnet.Objects;
using PhpbbInDotnet.Domain;
using System;
using System.Collections.Generic;

namespace PhpbbInDotnet.Forum.Pages.CustomPartials.Admin
{
    public class _AdminViewLogsPartialModel : PageModel
    {
        public OperationLogType? LogType { get; set; }
        
        public int LogPage { get; set; } = 1;

        public string? AuthorName { get; set; }

        public string Language { get; set; } = Constants.DEFAULT_LANGUAGE;

        public string? DateFormat { get; set; }

        public List<OperationLogSummary>? CurrentLogItems { get; set; }

        public int TotalLogItemCount { get; set; }

        public List<(DateTime LogDate, string? LogPath)>? SystemLogs { get; set; }
    }
}
