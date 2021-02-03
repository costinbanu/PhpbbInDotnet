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
using Serilog;
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

        public IEnumerable<PhpbbAttachments> Attachments { get; private set; }

        public bool IsAttachmentSearch { get; private set; }

        public List<KeyValuePair<string, int>> Users { get; set; }

        public IEnumerable<ExtendedPostDisplay> Posts { get; private set; }

        public Paginator Paginator { get; private set; }

        public bool IsAuthorSearch { get; private set; }

        private readonly string _searchFieldList;
        private readonly ILogger _logger;

        public SearchModel(ForumDbContext context, ForumTreeService forumService, UserService userService, IAppCache cache, IConfiguration config,
            AnonymousSessionCounter sessionCounter, CommonUtils utils, ILogger logger, LanguageProvider languageProvider)
            : base(context, forumService, userService, cache, config, sessionCounter, utils, languageProvider)
        {
            _searchFieldList =
                @"p.post_id,
				  p.post_subject,
				  p.post_text,
				  CASE WHEN p.poster_id = 1
					   THEN p.post_username 
				  	   ELSE u.username
			  	       END AS author_name,
				  p.poster_id AS author_id,
				  p.bbcode_uid,
				  from_unixtime(p.post_time) AS post_creation_time,
				  u.user_colour AS author_color,
				  u.user_avatar,
				  u.user_sig,
				  u.user_sig_bbcode_uid,
				  p.post_time,
                  p.forum_id";
            _logger = logger;
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

            var connection = Context.Database.GetDbConnection();

            Users = (
                await connection.QueryAsync("SELECT username, user_id FROM phpbb_users WHERE user_id <> @id AND user_type <> 2 ORDER BY username", new { id = Constants.ANONYMOUS_USER_ID })
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
            IsAuthorSearch = true;
            DoSearch = true;
            if (AuthorId == 0)
            {
                ModelState.AddModelError(nameof(SearchText), LanguageProvider.BasicText[await GetLanguage(), "AN_ERROR_OCCURRED_TRY_AGAIN"]);
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
                ModelState.AddModelError(nameof(SearchText), LanguageProvider.BasicText[await GetLanguage(), "AN_ERROR_OCCURRED_TRY_AGAIN"]);
                return await OnGet();
            }

            var fromJoinStmts =
                @"FROM phpbb_posts p
		          JOIN phpbb_users u ON p.poster_id = u.user_id
                  JOIN phpbb_attachments a ON a.post_msg_id = p.post_id";
            var whereStmt = 
                "WHERE p.poster_id = @authorId AND p.forum_id NOT IN @restrictedForumList";
            var orderLimitStmts =
                @"ORDER BY p.post_time DESC
                  LIMIT @skip, @take";

            var sql =
                $@"SELECT DISTINCT {_searchFieldList}
                    {fromJoinStmts}
                    {whereStmt}
                    {orderLimitStmts};
                    
                   SELECT count(distinct p.post_id) AS total_count 
                    {fromJoinStmts}
                    {whereStmt};

                   SELECT a.*
                    {fromJoinStmts}
                    {whereStmt}
                    {orderLimitStmts};";

            var connection = Context.Database.GetDbConnection();
            using var multi = await connection.QueryMultipleAsync(
               sql,
                new 
                { 
                    AuthorId,
                    skip = (PageNum.Value - 1) * Constants.DEFAULT_PAGE_SIZE,
                    take = Constants.DEFAULT_PAGE_SIZE,
                    restrictedForumList = (await ForumService.GetRestrictedForumList(await GetCurrentUserAsync(), true)).Select(f => f.forumId).DefaultIfEmpty()
                }
            );

            try
            {
                Posts = await multi.ReadAsync<ExtendedPostDisplay>();
                TotalResults = unchecked((int)await multi.ReadFirstOrDefaultAsync<long>());
                Attachments = await multi.ReadAsync<PhpbbAttachments>();
                Paginator = new Paginator(count: TotalResults.Value, pageNum: PageNum.Value, link: GetSearchLinkForPage(PageNum.Value + 1), topicId: null);

                return await OnGet();
            }
            catch
            {
                _logger.Error("Failed to perform post search. Query: {sql}", sql);
                throw;
            }
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
                ModelState.AddModelError(nameof(SearchText), LanguageProvider.Errors[await GetLanguage(), "MISSING_REQUIRED_FIELD"]);
                return;
            }

            var connection = Context.Database.GetDbConnection();
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

            static string GetWhereClause(string match)
                => $@"p.forum_id IN @forums
                  AND (@topic IS NULL OR @topic = p.topic_id)
                  AND (@author IS NULL OR @author = p.poster_id)
                  AND (@search IS NULL OR MATCH({match}) AGAINST(@search IN BOOLEAN MODE))";

            var sql =
                $@"SELECT DISTINCT {_searchFieldList}
                     FROM phpbb_posts p
 		             JOIN phpbb_users u ON p.poster_id = u.user_id
 		            WHERE {GetWhereClause("p.post_text")}

                    UNION

                   SELECT DISTINCT {_searchFieldList}
                     FROM phpbb_posts p
 		             JOIN phpbb_users u ON p.poster_id = u.user_id
 		            WHERE {GetWhereClause("p.post_subject")}

                	ORDER BY post_time DESC
	                LIMIT @skip, @take;

                   	WITH search_stmt AS (
		                SELECT DISTINCT p.post_id
		                  FROM phpbb_posts p
	                     WHERE {GetWhereClause("p.post_text")}
		 
                         UNION            
		
		                SELECT DISTINCT p.post_id
		                  FROM phpbb_posts p
		                 WHERE {GetWhereClause("p.post_subject")}
                    )
	                SELECT count(1) as total_count
                      FROM search_stmt;";

            using var multi = await connection.QueryMultipleAsync(
                sql,
                new
                {
                    topic = TopicId > 0 ? TopicId : null,
                    author = AuthorId > 0 ? AuthorId : null as int?,
                    search = string.IsNullOrWhiteSpace(SearchText) ? null : HttpUtility.UrlDecode(SearchText),
                    skip = (PageNum.Value - 1) * Constants.DEFAULT_PAGE_SIZE,
                    take = Constants.DEFAULT_PAGE_SIZE,
                    forums = forumIds.DefaultIfEmpty()
                }
            );

            try
            {
                Posts = await multi.ReadAsync<ExtendedPostDisplay>();
                TotalResults = unchecked((int)await multi.ReadFirstOrDefaultAsync<long>());
                Attachments = await connection.QueryAsync<PhpbbAttachments>("SELECT * FROM phpbb_attachments WHERE post_msg_id IN @postIds", new { postIds = Posts.Select(p => p.PostId ?? 0).DefaultIfEmpty() });

                Paginator = new Paginator(count: TotalResults.Value, pageNum: PageNum.Value, link: GetSearchLinkForPage(PageNum.Value + 1), topicId: null);
            }
            catch
            {
                _logger.Error("Failed to perform post search. Query: {sql}", sql);
                throw;
            }
        }

        public class ExtendedPostDisplay : PostDto
        {
            public string UserAvatar { get; set; }
            public int ForumId { get; set; }
        }
    }
}