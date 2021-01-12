using Microsoft.AspNetCore.Mvc.RazorPages;
using PhpbbInDotnet.Utilities;
using System;

namespace PhpbbInDotnet.Forum.Pages.CustomPartials
{
    public class _PaginationControlsPartialModel : PageModel
    {
        public int? TopicId { get; }
        
        public int? FirstPostId { get; }
        
        public bool IncludeEasyNavigation { get; }
        
        public string Back { get; }
        
        public string Forward { get; }
        
        public Paginator Paginator { get; }
        
        public string Self { get; }
       
        public bool AllowPaginationChange { get; }

        public string Language { get; }

        public _PaginationControlsPartialModel(Paginator paginator, bool allowPaginationChange, string back, string forward, bool includeEasyNavigation, string language, int? topicId = null, int? firstPostId = null) 
        {
            Paginator = paginator;
            Back = back;
            Forward = forward;
            TopicId = topicId;
            FirstPostId = firstPostId;
            IncludeEasyNavigation = includeEasyNavigation;
            AllowPaginationChange = allowPaginationChange;
            Self = Guid.NewGuid().ToString("n");
            Language = language;
        }
    }
}