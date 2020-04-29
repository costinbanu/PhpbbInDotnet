using Microsoft.AspNetCore.Mvc.RazorPages;
using System;

namespace Serverless.Forum.Pages.CustomPartials
{
    public class _PaginationControlsPartialModel : PageModel
    {
        public int? TopicId { get; private set; }
        public int? FirstPostId { get; private set; }
        public bool IncludeEasyNavigation { get; private set; }
        public string Back { get; private set; }
        public string Forward { get; private set; }
        public ModelWithPagination PaginationModel { get; private set; }
        public string Self { get; private set; }
        public bool AllowPaginationChange { get; private set; }

        public _PaginationControlsPartialModel(ModelWithPagination pagination, bool allowPaginationChange, string back, string forward, bool includeEasyNavigation, int? topicId = null, int? firstPostId = null) 
        {
            PaginationModel = pagination;
            Back = back;
            Forward = forward;
            TopicId = topicId;
            FirstPostId = firstPostId;
            IncludeEasyNavigation = includeEasyNavigation;
            AllowPaginationChange = allowPaginationChange;
            Self = Guid.NewGuid().ToString("n");
        }
    }
}