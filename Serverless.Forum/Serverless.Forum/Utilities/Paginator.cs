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
            PostsPerPage = new List<SelectListItem>
            {
                new SelectListItem("7", "7", false),
                new SelectListItem("14", "14", false),
                new SelectListItem("28", "28", false),
                new SelectListItem("56", "56", false),
                new SelectListItem("112", "112", false)
            };

            PageSize = 14;

            if (topicId != null && usr != null)
            {
                PageSize = usr.TopicPostsPerPage.ContainsKey(topicId.Value) ? usr.TopicPostsPerPage[topicId.Value] : 14;
            }

            IsAnonymous = (usr?.UserId ?? Constants.ANONYMOUS_USER_ID) == Constants.ANONYMOUS_USER_ID;

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
