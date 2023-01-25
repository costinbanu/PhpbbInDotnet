using Dapper;
using Microsoft.AspNetCore.Mvc;
using PhpbbInDotnet.Database;
using PhpbbInDotnet.Database.Entities;
using PhpbbInDotnet.Domain;
using PhpbbInDotnet.Forum.Models;
using PhpbbInDotnet.Languages;
using PhpbbInDotnet.Objects;
using PhpbbInDotnet.Services;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Linq;
using System.Threading.Tasks;

namespace PhpbbInDotnet.Forum.Pages
{
    public class IPLookupModel : AuthenticatedPageModel
    {
        public IPLookupModel(IForumTreeService forumService, IUserService userService, ISqlExecuter sqlExecuter, ITranslationProvider translationProvider)
            : base(forumService, userService, sqlExecuter, translationProvider)
        { }

        [BindProperty(SupportsGet = true)]
        [Required]
        public string? IP { get; set; }

        [BindProperty(SupportsGet = true)]
        public int PageNum { get; set; } = 1;

        public List<PostDto>? Posts { get; private set; }
        public int TotalCount { get; private set; }
        public List<PhpbbAttachments>? Attachments { get; private set; }
        public Paginator? Paginator { get; private set; }

        public Task<IActionResult> OnGet()
            => WithModerator(0, async () =>
            {
                var searchableForums = await ForumService.GetUnrestrictedForums(ForumUser, ignoreForumPassword: await UserService.IsAdmin(ForumUser));

                var searchTask = SqlExecuter.QueryAsync<PostDto>(
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
                     WHERE p.forum_id IN @searchableForums
                       AND p.poster_ip = @ip   
                     ORDER BY post_time DESC
                     LIMIT @skip, 14;",
                    new
                    {
                        Constants.ANONYMOUS_USER_ID,
                        IP,
                        skip = (PageNum - 1) * Constants.DEFAULT_PAGE_SIZE,
                        searchableForums
                    });
                var countTask = SqlExecuter.ExecuteScalarAsync<int>(
                    @"WITH search_stmt AS (
		                SELECT p.post_id
		                  FROM phpbb_posts p
                         WHERE p.forum_id IN @searchableForums
                           AND p.poster_ip = @ip 
                    )
	                SELECT count(1) as total_count
                      FROM search_stmt;",
                    new
                    {
                        IP,
                        searchableForums
                    });
                await Task.WhenAll(searchTask, countTask);

                Posts = (await searchTask).AsList();
                TotalCount = await countTask;
                Attachments = (await SqlExecuter.QueryAsync<PhpbbAttachments>(
                    @"SELECT *
                        FROM phpbb_attachments
                       WHERE post_msg_id IN @postIds",
                    new 
                    { 
                        postIds = Posts.Select(p => p.PostId).DefaultIfEmpty() 
                    })).AsList();
                Paginator = new Paginator(count: TotalCount, pageNum: PageNum, link: $"/IPLookup?ip={IP}&pageNum={PageNum + 1}", topicId: null);

                return Page();
            });
    }
}
