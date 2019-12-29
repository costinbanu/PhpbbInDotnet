using Microsoft.AspNetCore.Mvc.RazorPages;
using System;

namespace Serverless.Forum.Pages.CustomPartials
{
    public class _PaginationControlsPartialModel : PageModel
    {
        public int? TopicId { get; private set; }
        public int? LastPostId { get; private set; }
        public bool IncludeEasyNavigation { get; private set; }
        public string Back { get; private set; }
        public string Forward { get; private set; }
        public ModelWithPagination Pagination { get; private set; }
        public string Self { get; private set; }
        public bool AllowPaginationChange { get; private set; }

        public _PaginationControlsPartialModel(ModelWithPagination pagination, bool allowPaginationChange, string back, string forward, bool includeEasyNavigation, int? topicId = null, int? lastPostId = null) 
        {
            Pagination = pagination;
            Back = back;
            Forward = forward;
            TopicId = topicId;
            LastPostId = lastPostId;
            IncludeEasyNavigation = includeEasyNavigation;
            AllowPaginationChange = allowPaginationChange;
            Self = Guid.NewGuid().ToString("n");
        }
    }
}