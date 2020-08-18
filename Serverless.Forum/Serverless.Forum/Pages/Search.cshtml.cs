using Dapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Serverless.Forum.Contracts;
using Serverless.Forum.ForumDb;
using Serverless.Forum.Services;
using Serverless.Forum.Utilities;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Web;

namespace Serverless.Forum.Pages
{
    [ValidateAntiForgeryToken, ResponseCache(Location = ResponseCacheLocation.None, NoStore = true, Duration = 0)]
    public class SearchModel : ModelWithLoggedUser
    {
        [BindProperty(SupportsGet = true)]
        public string QueryString { get; set; }

        [BindProperty(SupportsGet = true)]
        public string Author { get; set; }

        [BindProperty(SupportsGet = true)]
        public int AuthorId { get; set; }

        [BindProperty(SupportsGet = true)]
        public int? ForumId { get; set; }

        [BindProperty(SupportsGet = true)]
        public int? TopicId { get; set; }

        [BindProperty(SupportsGet = true)]
        public string SearchText { get; set; }

        [BindProperty(SupportsGet = true)]
        public int? PageNum { get; set; }

        [BindProperty(SupportsGet = true)]
        public int? TotalResults { get; set; }

        [BindProperty(SupportsGet = true)]
        public bool? DoSearch { get; set; }

        public List<KeyValuePair<string, int>> Users { get; set; }

        public IEnumerable<ExtendedPostDisplay> Posts { get; private set; }

        public Paginator Paginator { get; private set; }
        public bool IsAuthorSearch { get; private set; }

        public SearchModel(ForumDbContext context, ForumTreeService forumService, UserService userService, CacheService cacheService)
            : base(context, forumService, userService, cacheService)
        { }

        public async Task OnGet()
        {
            NameValueCollection query = null;

            if (!string.IsNullOrWhiteSpace(QueryString))
            {
                query = HttpUtility.ParseQueryString(HttpUtility.UrlDecode(QueryString).ToLowerInvariant());
            }

            if (ForumId == null && query != null)
            {
                ForumId = int.TryParse(query["forumid"], out var i) ? i as int? : null;
            }

            if (TopicId == null && query != null)
            {
                TopicId = int.TryParse(query["topicid"], out var i) ? i as int? : null;
            }

            Users = await (
                from u in _context.PhpbbUsers.AsNoTracking()
                where u.UserId != Constants.ANONYMOUS_USER_ID && u.UserType != 2
                orderby u.Username
                select KeyValuePair.Create(u.Username, u.UserId)
            ).ToListAsync();

            if (ForumId == null && TopicId != null)
            {
                ForumId = (await _context.PhpbbTopics.AsNoTracking().FirstOrDefaultAsync(t => t.TopicId == TopicId))?.ForumId;
            }

            if (ForumId == null && TopicId == null && query != null)
            {
                var postId = int.TryParse(query["postid"], out var i) ? i as int? : null;
                var post = await _context.PhpbbPosts.AsNoTracking().FirstOrDefaultAsync(t => t.PostId == postId);
                TopicId = post?.TopicId;
                ForumId = post?.ForumId;
            }

            if (DoSearch ?? false)
            {
                await Search();
            }
        }

        public async Task OnGetByAuthor()
        {
            IsAuthorSearch = true;
            await OnGet();
            await Search();
        }

        public async Task OnPost()
        {
            await OnGet();
            await Search();
        }

        public string GetSearchLinkForPage(int page) =>
            $"/Search" +
                $"?{nameof(QueryString)}={HttpUtility.UrlEncode(QueryString)}" +
                $"&{nameof(Author)}={Author}" +
                $"&{nameof(ForumId)}={ForumId}" +
                $"&{nameof(TopicId)}={TopicId}" +
                $"&{nameof(SearchText)}={HttpUtility.UrlEncode(SearchText)}" +
                $"&{nameof(PageNum)}={page}" +
                $"&{nameof(TotalResults)}={TotalResults}" +
                $"&{nameof(DoSearch)}={true}";

        private async Task Search()
        {
            if (string.IsNullOrWhiteSpace(SearchText) && !IsAuthorSearch)
            {
                ModelState.AddModelError(nameof(SearchText), "Introduceți unul sau mai multe cuvinte!");
                return;
            }
            var restrictedForums = (await _forumService.GetRestrictedForumList(await GetCurrentUserAsync())).Select(f => f.forumId).ToList();
            restrictedForums.Remove(ForumId ?? -1);
            if (!restrictedForums.Any())
            {
                restrictedForums.Add(0);
            }

            using (var connection = _context.Database.GetDbConnection())
            {
                await connection.OpenIfNeeded();
                PageNum ??= 1;
                using var multi = await connection.QueryMultipleAsync(
                    "CALL `forum`.`search_post_text`(@forum, @topic, @author, @page, @excluded_forums, @search);",
                    new
                    {
                        forum = ForumId > 0 ? ForumId : null,
                        topic = TopicId > 0 ? TopicId : null,
                        author = AuthorId > 0 ? AuthorId : null as int?,
                        page = PageNum,
                        excluded_forums = string.Join(",", restrictedForums),
                        search = string.IsNullOrWhiteSpace(SearchText) ? null : HttpUtility.UrlDecode(SearchText)
                    }
                );

                Posts = await multi.ReadAsync<ExtendedPostDisplay>();

                TotalResults = unchecked((int)(await multi.ReadAsync<long>()).Single());
            }

            Paginator = new Paginator(count: TotalResults.Value, pageNum: PageNum.Value, link: GetSearchLinkForPage(PageNum.Value + 1), topicId: null);
        }

        public class ExtendedPostDisplay : PostDto
        {
            public string UserAvatar { get; set; }
            public int ForumId { get; set; }
            public string TopicTitle { get; set; }
        }
    }
}