using Microsoft.AspNetCore.Mvc.Rendering;
using Serverless.Forum.ForumDb;
using Serverless.Forum.Pages.CustomPartials;
using Serverless.Forum.Services;
using Serverless.Forum.Utilities;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Serverless.Forum
{
    public class ModelWithPagination : ModelWithLoggedUser
    {
        public _PaginationPartialModel Pagination { get; protected set; }
        public bool IsFirstPage { get; protected set; }
        public bool IsLastPage { get; protected set; }
        public int CurrentPage { get; protected set; }
        public readonly List<SelectListItem> PostsPerPage;

        public ModelWithPagination(ForumDbContext context, ForumTreeService forumService, UserService userService, CacheService cacheService)
            : base(context, forumService, userService, cacheService)
        {
            PostsPerPage = new List<SelectListItem>
            {
                new SelectListItem("7", "7", false),
                new SelectListItem("14", "14", false),
                new SelectListItem("28", "28", false),
                new SelectListItem("56", "56", false),
                new SelectListItem("112", "112", false)
            };
        }

        protected async Task ComputePagination(int count, int pageNum, string link, int? topicId = null)
        {
            var pageSize = 14;
            if (topicId.HasValue)
            {
                var usr = await GetCurrentUserAsync();
                pageSize = usr.TopicPostsPerPage.ContainsKey(topicId.Value) ? usr.TopicPostsPerPage[topicId.Value] : 14;
            }
            PostsPerPage.ForEach(ppp => ppp.Selected = int.TryParse(ppp.Value, out var value) && value == pageSize);

            Pagination = new _PaginationPartialModel(link, count, pageSize, pageNum);

            var noOfPages = (count / pageSize) + (count % pageSize == 0 ? 0 : 1);
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
