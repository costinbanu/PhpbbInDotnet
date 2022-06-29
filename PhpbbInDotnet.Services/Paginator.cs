using Microsoft.AspNetCore.Mvc.Rendering;
using PhpbbInDotnet.Objects;
using System.Collections.Generic;

namespace PhpbbInDotnet.Domain
{
    public class Paginator
    {
        public PaginationDto? Pagination { get; private set; }

        public bool IsFirstPage { get; private set; }

        public bool IsLastPage { get; private set; }

        public int CurrentPage { get; private set; }

        public List<SelectListItem> PostsPerPage { get; }

        public bool IsAnonymous { get; }

        public int PageSize { get; }

        public Paginator(int count, int pageNum, string link, int? pageSize = null, string pageNumKey = "PageNum")
        {
            PostsPerPage = new List<SelectListItem>();
            PageSize = pageSize ?? Constants.DEFAULT_PAGE_SIZE;
            IsAnonymous = true;

            Init(count, pageNum, link, pageNumKey);
        }

        public Paginator(int count, int pageNum, string link, int? topicId = null, AuthenticatedUserExpanded? usr = null, string pageNumKey = "PageNum")
        {
            PageSize = Constants.DEFAULT_PAGE_SIZE;

            if (topicId is not null && usr is not null)
            {
                PageSize = usr.GetPageSize(topicId.Value);
            }

            PostsPerPage = new List<SelectListItem>(5);
            for (var i = 0; i < 5; i++)
            {
                var val = (1 << i) * Constants.PAGE_SIZE_INCREMENT;
                var stringVal = val.ToString();
                PostsPerPage.Add(new SelectListItem(stringVal, stringVal, val == PageSize));
            }

            IsAnonymous = usr?.IsAnonymous ?? true;

            Init(count, pageNum, link, pageNumKey);
        }

        void Init(int count, int pageNum, string link, string pageNumKey)
        {
            Pagination = new PaginationDto(link, count, PageSize, pageNum, pageNumKey);
            var pageCount = (count / PageSize) + (count % PageSize == 0 ? 0 : 1);

            pageNum = NormalizePageNumberLowerBound(pageNum);
            if (pageNum > pageCount && pageCount > 0)
            {
                pageNum = pageCount;
            }

            IsFirstPage = pageNum == 1;
            IsLastPage = pageNum == pageCount;
            CurrentPage = pageNum;
        }

        public static int NormalizePageNumberLowerBound(int? candidate)
            => candidate >= 1 == false ? 1 : candidate.Value;
    }
}
