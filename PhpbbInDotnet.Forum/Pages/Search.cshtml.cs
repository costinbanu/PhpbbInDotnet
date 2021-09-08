using Dapper;
using LazyCache;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using PhpbbInDotnet.Database;
using PhpbbInDotnet.Database.Entities;
using PhpbbInDotnet.Languages;
using PhpbbInDotnet.Objects;
using PhpbbInDotnet.Services;
using PhpbbInDotnet.Utilities;
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

        public List<PhpbbAttachments> Attachments { get; private set; }

        public bool IsAttachmentSearch { get; private set; }

        public List<KeyValuePair<string, int>> Users { get; set; }

        public List<PostDto> Posts { get; private set; }

        public Paginator Paginator { get; private set; }

        public bool IsAuthorSearch { get; private set; }

        public SearchModel(ForumDbContext context, ForumTreeService forumService, UserService userService, IAppCache cache, CommonUtils utils, LanguageProvider languageProvider)
            : base(context, forumService, userService, cache, utils, languageProvider)
        {

        }

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

            var connection = await Context.GetDbConnectionAsync();

            Users = await UserService.GetUserMap();

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
            IsAuthorSearch = true;
            DoSearch = true;
            if (AuthorId == 0)
            {
                ModelState.AddModelError(nameof(SearchText), LanguageProvider.BasicText[GetLanguage(), "AN_ERROR_OCCURRED_TRY_AGAIN"]);
                return await OnGet();
            }
            return await OnGet();
        }

        public async Task<IActionResult> OnGetAttachments()
        {
            IsAttachmentSearch = true;
            DoSearch = false;
            PageNum ??= 1;

            if (AuthorId == 0)
            {
                ModelState.AddModelError(nameof(SearchText), LanguageProvider.BasicText[GetLanguage(), "AN_ERROR_OCCURRED_TRY_AGAIN"]);
                return await OnGet();
            }

            var connection = await Context.GetDbConnectionAsync();
            using var multi = await connection.QueryMultipleAsync(
                sql: "CALL search_user_attachments(@forums, @AuthorId, @page)",
                param: new 
                { 
                    AuthorId,
                    page = PageNum ?? 1,
                    forums = string.Join(',', (await ForumService.GetRestrictedForumList(GetCurrentUser(), true)).Select(f => f.forumId).DefaultIfEmpty())
                }
            );

            Posts = (await multi.ReadAsync<PostDto>()).AsList();
            TotalResults = unchecked((int)await multi.ReadFirstOrDefaultAsync<long>());
            Attachments = (await multi.ReadAsync<PhpbbAttachments>()).AsList();
            Paginator = new Paginator(count: TotalResults.Value, pageNum: PageNum.Value, link: GetSearchLinkForPage(PageNum.Value + 1), topicId: null);

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
                (IsAuthorSearch ? "&handler=byAuthor" : (IsAttachmentSearch ? "&handler=Attachments" : ""));

        private async Task Search()
        {
            if (string.IsNullOrWhiteSpace(SearchText) && !IsAuthorSearch)
            {
                ModelState.AddModelError(nameof(SearchText), LanguageProvider.Errors[GetLanguage(), "MISSING_REQUIRED_FIELD"]);
                return;
            }

            var connection = await Context.GetDbConnectionAsync();
            PageNum ??= 1;

            var (tree, _) = await GetForumTree(false, false);
            var forumIds = new List<int>(tree.Count);

            if ((ForumId ?? 0) > 0)
            {
                void traverse(int fid)
                {
                    var node = ForumService.GetTreeNode(tree, fid);
                    if (node != null)
                    {
                        if (!ForumService.IsNodeRestricted(node))
                        {
                            forumIds.Add(fid);
                        }
                        foreach (var child in node?.ChildrenList ?? new HashSet<int>())
                        {
                            traverse(child);
                        }
                    }
                }
                traverse(ForumId.Value);
            }
            else
            {
                forumIds.AddRange(tree.Where(t => !ForumService.IsNodeRestricted(t)).Select(t => t.ForumId));
            }

            using var multi = await connection.QueryMultipleAsync(
                sql: "CALL search_post_text(@forums, @topic, @author, @page, @search)",
                param: new
                {
                    topic = TopicId > 0 ? TopicId : null,
                    author = AuthorId > 0 ? AuthorId : null as int?,
                    search = string.IsNullOrWhiteSpace(SearchText) ? null : HttpUtility.UrlDecode(SearchText),
                    page = PageNum ?? 1,
                    forums = string.Join(',', forumIds.DefaultIfEmpty())
                }
            );

            Posts = (await multi.ReadAsync<PostDto>()).AsList();
            TotalResults = unchecked((int)await multi.ReadFirstOrDefaultAsync<long>());
            Attachments = (await multi.ReadAsync<PhpbbAttachments>()).AsList();
            Paginator = new Paginator(count: TotalResults.Value, pageNum: PageNum.Value, link: GetSearchLinkForPage(PageNum.Value + 1), topicId: null);
        }
    }
}