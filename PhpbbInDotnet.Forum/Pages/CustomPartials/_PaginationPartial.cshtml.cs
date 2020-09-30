using JW;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System;
using System.Collections.Specialized;
using System.Web;

namespace PhpbbInDotnet.Forum.Pages.CustomPartials
{
    public class _PaginationPartialModel : PageModel
    {
        public NameValueCollection Link { get; }
        public int Posts { get; }
        public int PostsPerPage { get; }
        public int CurrentPage { get; }
        public Pager Pager { get; }
        public bool HasPages => Pager.TotalPages > 1;
        public string PageNumKey { get; }

        public _PaginationPartialModel(string link, int posts, int postsPerPage, int currentPage = 1, string pageNumKey = "PageNum")
        {
            Link = HttpUtility.ParseQueryString(link);
            Posts = posts;
            PostsPerPage = postsPerPage;
            CurrentPage = currentPage;
            Pager = new Pager(Posts, CurrentPage, PostsPerPage, 7);
            PageNumKey = pageNumKey;
        }

        public void OnGet()
        {

        }
    }
}