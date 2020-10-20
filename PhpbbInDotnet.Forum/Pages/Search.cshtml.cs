using Dapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using PhpbbInDotnet.Objects;
using PhpbbInDotnet.Database;
using PhpbbInDotnet.Services;
using PhpbbInDotnet.Utilities;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using PhpbbInDotnet.Database.Entities;

namespace PhpbbInDotnet.Forum.Pages
{
    [ValidateAntiForgeryToken]
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

        public SearchModel(ForumDbContext context, ForumTreeService forumService, UserService userService, CacheService cacheService, IConfiguration config, 
            AnonymousSessionCounter sessionCounter, CommonUtils utils)
            : base(context, forumService, userService, cacheService, config, sessionCounter, utils)
        { }

        public async Task<IActionResult> OnGet()
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

            var connection = _context.Database.GetDbConnection();
            await connection.OpenIfNeededAsync();

            Users = (
                await connection.QueryAsync("SELECT username, user_id FROM phpbb_users WHERE user_id <> @id AND user_type <> 2", new { id = Constants.ANONYMOUS_USER_ID })
            ).Select(u => KeyValuePair.Create((string)u.username, (int)u.user_id)).ToList();

            if (ForumId == null && TopicId != null)
            {
                ForumId = (await connection.QueryFirstOrDefaultAsync<PhpbbTopics>("SELECT * FROM phpbb_topics WHERE topic_id = @topicId", new { TopicId }))?.ForumId;
            }

            if (ForumId == null && TopicId == null && query != null)
            {
                var postId = int.TryParse(query["postid"], out var i) ? i as int? : null;
                var post = await connection.QueryFirstOrDefaultAsync<PhpbbPosts>("SELECT * FROM phpbb_posts WHERE post_id = @postId", new { postId });
                TopicId = post?.TopicId;
                ForumId = post?.ForumId;
            }

            if (DoSearch ?? false)
            {
                await Search();
            }

            return Page();
        }

        public async Task<IActionResult> OnGetByAuthor()
        {
            if (AuthorId == 0)
            {
                return BadRequest("IDul utilizatorului este greșit.");
            }
            IsAuthorSearch = true;
            DoSearch = true;
            return await OnGet();
        }

        public async Task<IActionResult> OnPost()
        {
            DoSearch = true;
            return await OnGet();
        }

        public string GetSearchLinkForPage(int page) =>
            $"/Search" +
                $"?{nameof(QueryString)}={HttpUtility.UrlEncode(QueryString)}" +
                $"&{nameof(Author)}={Author}" +
                $"&{nameof(AuthorId)}={AuthorId}" +
                $"&{nameof(ForumId)}={ForumId}" +
                $"&{nameof(TopicId)}={TopicId}" +
                $"&{nameof(SearchText)}={HttpUtility.UrlEncode(SearchText)}" +
                $"&{nameof(PageNum)}={page}" +
                $"&{nameof(TotalResults)}={TotalResults}" +
                $"&{nameof(DoSearch)}={true}" +
                (IsAuthorSearch ? "&handler=byAuthor" : "");

        private async Task Search()
        {
            if (string.IsNullOrWhiteSpace(SearchText) && !IsAuthorSearch)
            {
                ModelState.AddModelError(nameof(SearchText), "Introduceți unul sau mai multe cuvinte!");
                return;
            }
            var restrictedForums = (await _forumService.GetRestrictedForumList(await GetCurrentUserAsync())).Where(f => f.forumId != ForumId);

            var connection = _context.Database.GetDbConnection();
            await connection.OpenIfNeededAsync();

            PageNum ??= 1;
            using var multi = await connection.QueryMultipleAsync(
                "CALL `forum`.`search_post_text`(@forum, @topic, @author, @page, @excluded_forums, @search);",
                new
                {
                    forum = ForumId > 0 ? ForumId : null,
                    topic = TopicId > 0 ? TopicId : null,
                    author = AuthorId > 0 ? AuthorId : null as int?,
                    page = PageNum,
                    excluded_forums = string.Join(',', restrictedForums.Select(f => f.forumId).DefaultIfEmpty()),
                    search = string.IsNullOrWhiteSpace(SearchText) ? null : HttpUtility.UrlDecode(SearchText)
                }
            );

            Posts = await multi.ReadAsync<ExtendedPostDisplay>();

            TotalResults = unchecked((int)(await multi.ReadAsync<long>()).Single());

            Paginator = new Paginator(count: TotalResults.Value, pageNum: PageNum.Value, link: GetSearchLinkForPage(PageNum.Value + 1), topicId: null);
        }

        public class ExtendedPostDisplay : PostDto
        {
            public string UserAvatar { get; set; }
            public int ForumId { get; set; }
        }
    }
}