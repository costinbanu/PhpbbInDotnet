using JW;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Collections.Specialized;
using System.Web;

namespace Serverless.Forum.Pages
{
    public class _PaginationPartialModel : PageModel
    {
        public NameValueCollection Link { get; private set; }
        public int Posts { get; private set; }
        public int PostsPerPage { get; private set; }
        public int CurrentPage { get; private set; }
        public Pager Pager { get; private set; }
        public bool HasPages => Pager.TotalPages > 1;

        public _PaginationPartialModel(string link, int posts, int postsPerPage, int currentPage = 1)
        {
            Link = HttpUtility.ParseQueryString(link);
            Posts = posts;
            PostsPerPage = postsPerPage;
            CurrentPage = currentPage;
            Pager = new Pager(Posts, CurrentPage, PostsPerPage, 7);
        }

        public void OnGet()
        {

        }
    }
}