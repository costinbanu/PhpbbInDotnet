using Microsoft.AspNetCore.Mvc.RazorPages;
using System;

namespace Serverless.Forum.Pages.CustomPartials
{
    public class _SummaryPartialModel : PageModel
    {
        public int? AuthorId { get; private set; }
        public string AuthorName { get; private set; }
        public string AuthorColor { get; private set; }
        public DateTime? CreationTime { get; private set; }
        public int AssetId { get; private set; }
        public string DateFormat { get; private set; }
        public bool ShowAvatar { get; private set; } = false;
        public string LinkHref { get; private set; }
        public string LinkText { get; private set; }
        public string LinkTooltip { get; private set; }
        public bool Left { get; private set; }

        public _SummaryPartialModel(int? authorId, string authorName, string authorColor, DateTime? creationTime, int assetId, string dateFormat, 
            bool showAvatar, string linkHref, string linkText, string linkTooltip, bool left)
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
        }
    }
}