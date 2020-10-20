using JW;
using System.Collections.Specialized;
using System.Web;

namespace PhpbbInDotnet.Objects
{
    public class PaginationDto
    {
        public NameValueCollection Link { get; }
        public int Posts { get; }
        public int PostsPerPage { get; }
        public int CurrentPage { get; }
        public Pager Pager { get; }
        public bool HasPages => Pager.TotalPages > 1;
        public string PageNumKey { get; }

        public PaginationDto(string link, int posts, int postsPerPage, int currentPage = 1, string pageNumKey = "PageNum")
        {
            Link = HttpUtility.ParseQueryString(link);
            Posts = posts;
            PostsPerPage = postsPerPage;
            CurrentPage = currentPage;
            Pager = new Pager(Posts, CurrentPage, PostsPerPage, 7);
            PageNumKey = pageNumKey;
        }
    }
}
