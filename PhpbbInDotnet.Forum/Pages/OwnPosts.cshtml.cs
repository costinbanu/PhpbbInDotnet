using Dapper;
using LazyCache;
using Microsoft.AspNetCore.Mvc;
using PhpbbInDotnet.Database;
using PhpbbInDotnet.Languages;
using PhpbbInDotnet.Objects;
using PhpbbInDotnet.Services;
using PhpbbInDotnet.Utilities;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PhpbbInDotnet.Forum.Pages
{
    public class OwnPostsModel : AuthenticatedPageModel
    {
        public List<TopicDto> Topics { get; private set; } = new List<TopicDto>();
        public Paginator? Paginator { get; private set; }

        [BindProperty(SupportsGet = true)]
        public int PageNum { get; set; } = 1;

        public OwnPostsModel(IForumDbContext context, IForumTreeService forumService, IUserService userService, IAppCache cache, ICommonUtils utils, LanguageProvider languageProvider)
            : base(context, forumService, userService, cache, utils, languageProvider)
        {
        }

        public async Task<IActionResult> OnGet()
            => await WithRegisteredUser(async (user) =>
            {
                await Utils.RetryOnceAsync(
                    toDo: async () =>
                    {
                        PageNum = Paginator.NormalizePageNumberLowerBound(PageNum);
                        var restrictedForumList = await GetRestrictedForums();
                        var topicsTask = Context.GetSqlExecuter().QueryAsync<TopicDto>(
                            @"WITH own_topics AS (
			                    SELECT DISTINCT p.topic_id
			                      FROM phpbb_posts p
			                      JOIN phpbb_topics t 
			                        ON p.topic_id = t.topic_id
			                     WHERE p.poster_id = @userId
                                   AND t.forum_id NOT IN @restrictedForumList
                                 ORDER BY t.topic_last_post_time DESC
                                 LIMIT @skip, @take
                            )
                            SELECT t.topic_id, 
			                       t.forum_id,
			                       t.topic_title, 
			                       count(p.post_id) AS post_count,
                                   t.topic_views AS view_count,
			                       t.topic_type,
			                       t.topic_last_poster_id,
			                       t.topic_last_poster_name,
			                       t.topic_last_post_time,
			                       t.topic_last_poster_colour,
			                       t.topic_last_post_id
                              FROM phpbb_posts p
                              JOIN own_topics ot
                                ON p.topic_id = ot.topic_id
                              JOIN phpbb_topics t
                                ON t.topic_id = ot.topic_id
                             GROUP BY p.topic_id
                             ORDER BY t.topic_last_post_time DESC",
                            new
                            {
                                user.UserId,
                                skip = (PageNum - 1) * Constants.DEFAULT_PAGE_SIZE,
                                take = Constants.DEFAULT_PAGE_SIZE,
                                restrictedForumList
                            }
                        );
                        var countTask = Context.GetSqlExecuter().ExecuteScalarAsync<int>(
                            @"SELECT COUNT(DISTINCT topic_id) AS total_count 
	                            FROM phpbb_posts 
	                           WHERE poster_id = @user_id
                                 AND forum_id NOT IN @restrictedForumList;",
                            new
                            {
                                user.UserId,
                                restrictedForumList
                            });

                        await Task.WhenAll(topicsTask, countTask);

                        Topics = (await topicsTask).AsList();
                        Paginator = new Paginator(count: await countTask, pageNum: PageNum, link: "/OwnPosts?pageNum=1", topicId: null);
                    },
                    evaluateSuccess: () => Topics!.Count > 0 && PageNum == Paginator!.CurrentPage,
                    fix: () => PageNum = Paginator!.CurrentPage);

                return Page();
            });
    }
}
