using Microsoft.AspNetCore.Mvc.RazorPages;
using PhpbbInDotnet.Utilities;
using System;

namespace PhpbbInDotnet.Forum.Pages.CustomPartials
{
    public class _SummaryPartialModel : PageModel
    {
        public int? AuthorId { get; set; }

        public string? AuthorName { get; set; }

        public string? AuthorColor { get; set; }

        public string? AuthorAvatar { get; set; }

        public DateTime? CreationTime { get; set; }

        public int AssetId { get; set; }

        public string? DateFormat { get; set; }

        public bool ShowAvatar => !string.IsNullOrWhiteSpace(AuthorAvatar);

        public string? LinkHref { get; set; }

        public bool Left { get; set; }

        public string? AuthorRank { get; set; }

        public bool ShowAsLast { get; set; }

        public int? Posts { get; set; }

        public int? Views { get; set; }

        public int? Forums { get; set; }

        public int? Topics { get; set; }

        public string? AuthorTag { get; set; }

        public string? PMLink { get; set; }

        public string Language { get; set; } = Constants.DEFAULT_LANGUAGE;

        public string? DateLabel { get; set; }

        public Guid? CorrelationId { get; set; }
    }
}