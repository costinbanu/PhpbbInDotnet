using Microsoft.AspNetCore.Mvc.RazorPages;
using System;

namespace PhpbbInDotnet.Forum.Pages.CustomPartials
{
    public class _SummaryPartialModel : PageModel
    {
        public int? AuthorId { get; set; }

        public string AuthorName { get; set; }

        public string AuthorColor { get; set; }

        public DateTime? CreationTime { get; set; }

        public int AssetId { get; set; }

        public string DateFormat { get; set; }

        public bool ShowAvatar { get; set; } = false;

        public string LinkHref { get; set; }

        //public string LinkText { get; set; }

        //public string LinkTooltip { get; set; }

        public bool Left { get; set; }

        public string AuthorRank { get; set; }

        public bool ShowAsLast { get; set; }

        public int? Posts { get; set; }

        public int? Views { get; set; }

        public int? Forums { get; set; }

        public int? Topics { get; set; }

        public string AuthorTag { get; set; } = "Autor: ";

        public string PMLink { get; set; }

        public string Language { get; set; }
    }
}