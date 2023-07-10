using Dapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using PhpbbInDotnet.Database.Entities;
using PhpbbInDotnet.Database.SqlExecuter;
using PhpbbInDotnet.Domain;
using PhpbbInDotnet.Domain.Utilities;
using PhpbbInDotnet.Forum.Models;
using PhpbbInDotnet.Languages;
using PhpbbInDotnet.Objects;
using PhpbbInDotnet.Services;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Web;

namespace PhpbbInDotnet.Forum.Pages
{
    [ValidateAntiForgeryToken]
    public class SearchModel : AuthenticatedPageModel
    {
        [BindProperty(SupportsGet = true)]
        public string? QueryString { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? Author { get; set; }

        [BindProperty(SupportsGet = true)]
        public int AuthorId { get; set; }

        [BindProperty(SupportsGet = true)]
        public int? ForumId { get; set; }

        [BindProperty(SupportsGet = true)]
        public int? TopicId { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? SearchText { get; set; }

        [BindProperty(SupportsGet = true)]
        public int PageNum { get; set; } = 1;

        [BindProperty(SupportsGet = true)]
        public int? TotalResults { get; set; }

        [BindProperty(SupportsGet = true)]
        public bool? DoSearch { get; set; }

        public IEnumerable<PhpbbAttachmentExpanded>? Attachments { get; private set; }

        public bool IsAttachmentSearch { get; private set; }

        public List<KeyValuePair<string, int>>? Users { get; set; }

        public List<PostDto>? Posts { get; private set; }

        public Paginator? Paginator { get; private set; }

        public bool IsAuthorSearch { get; private set; }

        public SearchModel(IForumTreeService forumService, IUserService userService, ISqlExecuter sqlExecuter, 
            ITranslationProvider translationProvider, IConfiguration configuration)
            : base(forumService, userService, sqlExecuter, translationProvider, configuration)
        { }

        public async Task<IActionResult> OnGet()
        {
            NameValueCollection? query = null;

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

            Users = await UserService.GetUserMap();

            if (ForumId == null && TopicId != null)
            {
                ForumId = (await SqlExecuter.QueryFirstOrDefaultAsync<PhpbbTopics>("SELECT * FROM phpbb_topics WHERE topic_id = @topicId", new { TopicId }))?.ForumId;
            }

            if (ForumId == null && TopicId == null && query != null)
            {
                var postId = int.TryParse(query["postid"], out var i) ? i as int? : null;
                var post = await SqlExecuter.QueryFirstOrDefaultAsync<PhpbbPosts>("SELECT * FROM phpbb_posts WHERE post_id = @postId", new { postId });
                TopicId = post?.TopicId;
                ForumId = post?.ForumId;
            }

            if (DoSearch ?? false)
            {
                PageNum = Paginator.NormalizePageNumberLowerBound(PageNum);
                await ResiliencyUtility.RetryOnceAsync(
                    toDo: () => Search(),
                    evaluateSuccess: () => (Posts?.Count ?? 0) > 0 && PageNum == Paginator?.CurrentPage,
                    fix: () => PageNum = Paginator.NormalizePageNumberLowerBound(Paginator?.CurrentPage));
            }

            return Page();
        }

        public Task<IActionResult> OnGetByAuthor()
            => WithRegisteredUser(async (_) =>
            {
                IsAuthorSearch = true;
                DoSearch = true;
                if (AuthorId == 0)
                {
                    return await PageWithErrorAsync(nameof(SearchText), TranslationProvider.BasicText[Language, "AN_ERROR_OCCURRED_TRY_AGAIN"], resultFactory: OnGet);
                }
                return await OnGet();
            });

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
                (IsAuthorSearch ? "&handler=byAuthor" : (IsAttachmentSearch ? "&handler=Attachments" : ""));

        private async Task Search()
        {
            if (string.IsNullOrWhiteSpace(SearchText) && !IsAuthorSearch)
            {
                ModelState.AddModelError(nameof(SearchText), TranslationProvider.Errors[Language, "MISSING_REQUIRED_FIELD"]);
                return;
            }

            var searchableForums = string.Join(",", await ForumService.GetUnrestrictedForums(ForumUser, ForumId ?? 0));
            var results = (await SqlExecuter.CallStoredProcedureAsync<PostDto>(
                "search_posts",
                new
                {
                    Constants.ANONYMOUS_USER_ID,
                    topicId = TopicId ?? 0,
                    AuthorId,
                    searchText = string.IsNullOrWhiteSpace(SearchText) ? string.Empty : HttpUtility.UrlDecode(SearchText),
                    searchableForums,
                    skip = (PageNum - 1) * Constants.DEFAULT_PAGE_SIZE,
                    take = Constants.DEFAULT_PAGE_SIZE
                })).AsList();

            Posts = results.Cast<PostDto>().ToList();
            TotalResults = results.Count == 0 ? 0 : results[0].TotalCount;
            Attachments = await SqlExecuter.QueryAsync<PhpbbAttachmentExpanded>(
                @"SELECT p.forum_id, a.*
                    FROM phpbb_attachments a
                    JOIN phpbb_posts p ON a.post_msg_id = p.post_id
                    WHERE p.post_id IN @posts",
                new { posts = Posts.Select(p => p.PostId).DefaultIfEmpty() });
            Paginator = new Paginator(count: TotalResults.Value, pageNum: PageNum, link: GetSearchLinkForPage(PageNum + 1), topicId: null);
        }
    }
}