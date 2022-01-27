using Dapper;
using LazyCache;
using Microsoft.AspNetCore.Mvc;
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
        public int? PageNum { get; set; }

        [BindProperty(SupportsGet = true)]
        public int? TotalResults { get; set; }

        [BindProperty(SupportsGet = true)]
        public bool? DoSearch { get; set; }

        public List<PhpbbAttachments>? Attachments { get; private set; }

        public bool IsAttachmentSearch { get; private set; }

        public List<KeyValuePair<string, int>>? Users { get; set; }

        public List<PostDto>? Posts { get; private set; }

        public Paginator? Paginator { get; private set; }

        public bool IsAuthorSearch { get; private set; }

        public SearchModel(ForumDbContext context, ForumTreeService forumService, UserService userService, IAppCache cache, CommonUtils utils, LanguageProvider languageProvider)
            : base(context, forumService, userService, cache, utils, languageProvider)
        {

        }

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

        public Task<IActionResult> OnGetByAuthor()
            => WithRegisteredUser(async (_) =>
            {
                IsAuthorSearch = true;
                DoSearch = true;
                if (AuthorId == 0)
                {
                    ModelState.AddModelError(nameof(SearchText), LanguageProvider.BasicText[GetLanguage(), "AN_ERROR_OCCURRED_TRY_AGAIN"]);
                    return await OnGet();
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
                ModelState.AddModelError(nameof(SearchText), LanguageProvider.Errors[GetLanguage(), "MISSING_REQUIRED_FIELD"]);
                return;
            }

            PageNum ??= 1;

            var connectionTask = Context.GetDbConnectionAsync();
            var restrictedForumsTask = ForumService.GetRestrictedForumList(GetCurrentUser());
            await Task.WhenAll(connectionTask, restrictedForumsTask);
            var connection = await connectionTask;
            var restrictedForums = (await restrictedForumsTask).Select(f => f.forumId);

            var searchTask = connection.QueryAsync<PostDto>(
                @"WITH ranks AS (
	                SELECT DISTINCT u.user_id, 
		                   COALESCE(r1.rank_id, r2.rank_id) AS rank_id, 
		                   COALESCE(r1.rank_title, r2.rank_title) AS rank_title
	                  FROM phpbb_users u
	                  JOIN phpbb_groups g ON u.group_id = g.group_id
	                  LEFT JOIN phpbb_ranks r1 ON u.user_rank = r1.rank_id
	                  LEFT JOIN phpbb_ranks r2 ON g.group_rank = r2.rank_id
                )
                SELECT p.forum_id,
	                   p.topic_id,
	                   p.post_id,
	                   p.post_subject,
	                   p.post_text,
	                   case when p.poster_id = @ANONYMOUS_USER_ID
			                then p.post_username 
			                else a.username
	                   end as author_name,
	                   p.poster_id as author_id,
	                   p.bbcode_uid,
	                   p.post_time,
	                   a.user_colour as author_color,
	                   a.user_avatar as author_avatar,
	                   p.post_edit_count,
	                   p.post_edit_reason,
	                   p.post_edit_time,
	                   e.username as post_edit_user,
	                   r.rank_title as author_rank,
	                   p.poster_ip as ip
                  FROM phpbb_posts p
                  JOIN phpbb_users a ON p.poster_id = a.user_id
                  LEFT JOIN phpbb_users e ON p.post_edit_user = e.user_id
                  LEFT JOIN ranks r ON a.user_id = r.user_id
                 WHERE p.forum_id NOT IN @restrictedForums
                   AND (@topicId = 0 OR @topicId = p.topic_id)
                   AND (@authorId = 0 OR @authorId = p.poster_id)
                   AND (@searchText IS NULL OR MATCH(p.post_text) AGAINST(@searchText IN BOOLEAN MODE))
  
                 UNION
  
                SELECT p.forum_id,
	                   p.topic_id,
	                   p.post_id,
	                   p.post_subject,
	                   p.post_text,
	                   case when p.poster_id = @ANONYMOUS_USER_ID
			                then p.post_username 
			                else a.username
	                   end as author_name,
	                   p.poster_id as author_id,
	                   p.bbcode_uid,
	                   p.post_time,
	                   a.user_colour as author_color,
	                   a.user_avatar as author_avatar,
	                   p.post_edit_count,
	                   p.post_edit_reason,
	                   p.post_edit_time,
	                   e.username as post_edit_user,
	                   r.rank_title as author_rank,
	                   p.poster_ip as ip
                  FROM phpbb_posts p
                  JOIN phpbb_users a ON p.poster_id = a.user_id
                  LEFT JOIN phpbb_users e ON p.post_edit_user = e.user_id
                  LEFT JOIN ranks r ON a.user_id = r.user_id
                 WHERE p.forum_id NOT IN @restrictedForums
                   AND (@topicId = 0 OR @topicId = p.topic_id)
                   AND (@authorId = 0 OR @authorId = p.poster_id)
                   AND (@searchText IS NULL OR MATCH(p.post_subject) AGAINST(@searchText IN BOOLEAN MODE))
   
                 ORDER BY post_time DESC
                 LIMIT @skip, 14;",
                new
                {
                    Constants.ANONYMOUS_USER_ID,
                    topicId = TopicId ?? 0,
                    AuthorId,
                    searchText = string.IsNullOrWhiteSpace(SearchText) ? null : HttpUtility.UrlDecode(SearchText),
                    skip = ((PageNum ?? 1) - 1) * 14,
                    restrictedForums = restrictedForums.DefaultIfEmpty()
                });
            var countTask = connection.ExecuteScalarAsync<int>(
                @"WITH search_stmt AS (
		            SELECT p.post_id
		              FROM phpbb_posts p
                     WHERE p.forum_id NOT IN @restrictedForums
                       AND (@topicId = 0 OR @topicId = p.topic_id)
                       AND (@authorId = 0 OR @authorId = p.poster_id)
                       AND (@searchText IS NULL OR MATCH(p.post_text) AGAINST(@searchText IN BOOLEAN MODE))
		 
                     UNION            
		
		            SELECT p.post_id
		              FROM phpbb_posts p
                     WHERE p.forum_id NOT IN @restrictedForums
                       AND (@topicId = 0 OR @topicId = p.topic_id)
                       AND (@authorId = 0 OR @authorId = p.poster_id)
                       AND (@searchText IS NULL OR MATCH(p.post_subject) AGAINST(@searchText IN BOOLEAN MODE))
                )
	            SELECT count(1) as total_count
                  FROM search_stmt;",
                new
                {
                    topicId = TopicId ?? 0,
                    AuthorId,
                    searchText = string.IsNullOrWhiteSpace(SearchText) ? null : HttpUtility.UrlDecode(SearchText),
                    restrictedForums = restrictedForums.DefaultIfEmpty()
                });
            await Task.WhenAll(searchTask, countTask);

            Posts = (await searchTask).AsList();
            TotalResults = await countTask;
            Attachments = await (
                from a in Context.PhpbbAttachments.AsNoTracking()
                where Posts.Select(p => p.PostId).Contains(a.PostMsgId)
                select a).ToListAsync();
            Paginator = new Paginator(count: TotalResults.Value, pageNum: PageNum!.Value, link: GetSearchLinkForPage(PageNum.Value + 1), topicId: null);
        }
    }
}