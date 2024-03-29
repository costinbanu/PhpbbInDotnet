using Microsoft.AspNetCore.Mvc.RazorPages;
using PhpbbInDotnet.Domain;
using PhpbbInDotnet.Objects;
using System;

namespace PhpbbInDotnet.Forum.Pages.CustomPartials
{
    public class _PostPartialModel : PageModel
    {
        public PostDto? Post { get; set; }

        public ForumUserExpanded? CurrentUser { get; set; }

        public int? ClosestPostId { get; set; }

        public int[]? PostIdsForModerator { get; set; }

        public bool HasCurrentUserPM { get; set; }

        public bool IsCurrentUserMod { get; set; }

        public bool IsPostFirstInPage { get; set; }

        public bool IsPostLastInPage { get; set; }

        public bool IsLastPage { get; set; }

        public bool IsTopicLocked { get; set; }

        public bool ShowFooter { get; set; }

        public bool ShowEditHistory { get; set; }

        public bool ShowQuoteButton { get; set; }

        public bool OpenPostLinkInNewTab { get; set; }

        public string? ToHighlight { get; set; }

        public string Language { get; set; } = Constants.DEFAULT_LANGUAGE;

        public Guid? CorrelationId { get; set; }

        public bool DisablePostTitleLink { get; set; }

        public string? Path { get; set; }

        public bool IsForumReadOnly { get; set; }
    }
}
