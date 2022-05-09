using Microsoft.AspNetCore.Mvc.RazorPages;
using System;

namespace PhpbbInDotnet.Forum.Pages.CustomPartials
{
    public class _SummaryPartialModel : PageModel
    {
        public int AuthorId { get; }

        public string AuthorName { get; }

        public string AuthorColor { get; }

        public string? AuthorAvatar { get; set; }

        public DateTime CreationTime { get; }

        public int AssetId { get; }

        public string DateFormat { get; }

        public string? LinkHref { get; set; }

        public bool AlignLeft { get; set; }

        public string? AuthorRank { get; set; }

        public bool IsLastPostSummary { get; set; }

        public int? Posts { get; set; }

        public int? Views { get; set; }

        public int? Forums { get; set; }

        public int? Topics { get; set; }

        public string? AuthorTag { get; set; }

        public string? PMLink { get; set; }

        public string Language { get; }

        public string? DateLabel { get; set; }

        public Guid? CorrelationId { get; set; }

        public bool AuthorOnFoeList { get; }

        public _SummaryPartialModel(int authorId, string authorName, string authorColor, DateTime creationTime, int assetId, string dateFormat, string language, bool authorOnFoeList)
        {
            AuthorId = authorId;
            AuthorName = authorName;
            AuthorColor = authorColor;
            CreationTime = creationTime;
            AssetId = assetId;
            DateFormat = dateFormat;
            Language = language;
            AuthorOnFoeList = authorOnFoeList;
        }
    }
}