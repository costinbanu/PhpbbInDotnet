﻿using Microsoft.AspNetCore.Mvc.RazorPages;
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

        public _SummaryPartialModel(int? authorId, string authorName, string authorColor, DateTime? creationTime, int assetId, string dateFormat, 
            bool showAvatar, string authorRank, string linkHref, string linkText, string linkTooltip, bool left)
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
        }
    }
}