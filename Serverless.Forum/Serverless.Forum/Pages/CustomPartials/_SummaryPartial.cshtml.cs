using Microsoft.AspNetCore.Mvc.RazorPages;
using System;

namespace Serverless.Forum.Pages.CustomPartials
{
    public class _SummaryPartialModel : PageModel
    {
        public int? AuthorId { get; }
        public string AuthorName { get; }
        public string AuthorColor { get; }
        public DateTime? CreationTime { get; }
        public int AssetId { get; }
        public string DateFormat { get; }
        public bool ShowAvatar { get; } = false;
        public string LinkHref { get; }
        public string LinkText { get; }
        public string LinkTooltip { get; }
        public bool Left { get; }
        public string AuthorRank { get; }
        public bool ShowAsLast { get; }
        public int? Posts { get; }
        public int? Views { get; }
        public string AuthorTag { get; }
        public string PMLink { get; }

        public _SummaryPartialModel(int? authorId, string authorName, string authorColor, DateTime? creationTime, int assetId, string dateFormat, 
            bool showAvatar, string authorRank, string linkHref, string linkText, string linkTooltip, bool left, bool showAsLast = false, int? posts = null, 
            int? views = null, string authorTag = null, string pmLink = null)
        {
            AuthorId = authorId;
            AuthorName = authorName;
            AuthorColor = authorColor;
            CreationTime = creationTime;
            AssetId = assetId;
            DateFormat = dateFormat;
            ShowAvatar = showAvatar;
            LinkHref = linkHref;
            LinkText = linkText;
            LinkTooltip = linkTooltip;
            Left = left;
            AuthorRank = authorRank;
            ShowAsLast = showAsLast;
            Posts = posts;
            Views = views;
            AuthorTag = authorTag ?? "Autor: ";
            PMLink = pmLink;
        }
    }
}