using JW;
using System;
using System.Collections.Specialized;
using System.Web;

namespace PhpbbInDotnet.Objects
{
    public class PaginationDto
    {
        private readonly string _url;
        private readonly NameValueCollection _queryVars;
        private readonly string _pageNumKey;


        public int Posts { get; }
        public int PostsPerPage { get; }
        public int CurrentPage { get; }
        public Pager Pager { get; }
        public bool HasPages => Pager.TotalPages > 1;

        public PaginationDto(string link, int posts, int postsPerPage, int currentPage = 1, string pageNumKey = "PageNum")
        {
            var idx = link.IndexOf('?');
            var query = idx >= 0 ? link[idx..] : "";
            _url = idx >= 0 ? link[..idx] : link;
            _queryVars = HttpUtility.ParseQueryString(query);
            _pageNumKey = pageNumKey;

            Posts = posts;
            PostsPerPage = postsPerPage;
            CurrentPage = currentPage;
            Pager = new Pager(Posts, CurrentPage, PostsPerPage, 7);
        }

        public void UpdatePageNum(string value)
        {
            _queryVars.Set(_pageNumKey, value);
        }

        public string Url => $"{_url}?{_queryVars}";
    }
}
