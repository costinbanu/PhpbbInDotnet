using Microsoft.AspNetCore.Mvc.Rendering;
using Serverless.Forum.Contracts;
using Serverless.Forum.Pages.CustomPartials;
using System.Collections.Generic;

namespace Serverless.Forum.Utilities
{
    public class Paginator
    {
        public _PaginationPartialModel Pagination { get; }

        public bool IsFirstPage { get; }

        public bool IsLastPage { get; }

        public int CurrentPage { get; }

        public List<SelectListItem> PostsPerPage { get; }

        public bool IsAnonymous { get; }

        public int PageSize { get; }

        public Paginator(int count, int pageNum, string link, int? topicId = null, LoggedUser usr = null, string pageNumKey = "PageNum")
        {
            PostsPerPage = new List<SelectListItem>();
            for (var i = 0; i < 5; i++)
            {
                var val = ((1 << i) * Constants.PAGE_SIZE_INCREMENT).ToString();
                PostsPerPage.Add(new SelectListItem(val, val, false));
            }

            PageSize = Constants.DEFAULT_PAGE_SIZE;

            if (topicId != null && usr != null)
            {
                PageSize = usr.TopicPostsPerPage.ContainsKey(topicId.Value) ? usr.TopicPostsPerPage[topicId.Value] : Constants.DEFAULT_PAGE_SIZE;
            }

            IsAnonymous = usr?.IsAnonymous ?? true;

            PostsPerPage.ForEach(ppp => ppp.Selected = int.TryParse(ppp.Value, out var value) && value == PageSize);
            Pagination = new _PaginationPartialModel(link, count, PageSize, pageNum, pageNumKey);

            var noOfPages = (count / PageSize) + (count % PageSize == 0 ? 0 : 1);
            if (pageNum > noOfPages)
            {
                pageNum = noOfPages;
            }
            if (pageNum < 1)
            {
                pageNum = 1;
            }

            IsFirstPage = pageNum == 1;
            IsLastPage = pageNum == noOfPages;
            CurrentPage = pageNum;
        }
    }
}
