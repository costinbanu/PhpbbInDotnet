using Dapper;
using LazyCache;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PhpbbInDotnet.Database;
using PhpbbInDotnet.Languages;
using PhpbbInDotnet.Objects;
using PhpbbInDotnet.Services;
using PhpbbInDotnet.Utilities;
using PhpbbInDotnet.Utilities.Extensions;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace PhpbbInDotnet.Forum.Pages
{
    public class DraftsModel : AuthenticatedPageModel
    {
        public List<TopicDto> Topics { get; private set; } = new List<TopicDto>();
        public Paginator? Paginator { get; private set; }

        [BindProperty(SupportsGet = true)]
        public int PageNum { get; set; } = 1;

        [BindProperty]
        public int[]? SelectedDrafts { get; set; }

        public DraftsModel(IForumDbContext context, IForumTreeService forumService, IUserService userService, IAppCache cache, ICommonUtils utils, LanguageProvider languageProvider)
            : base(context, forumService, userService, cache, utils, languageProvider)
        {
        }

        public async Task<IActionResult> OnGet()
            => await WithRegisteredUser(async (user) =>
            {
                await Utils.RetryOnceAsync(
                    toDo: async () =>
                    {
                        var restrictedForumList = await GetRestrictedForums();
                        PageNum = Paginator.NormalizePageNumberLowerBound(PageNum);
                        var draftsTask = Context.GetSqlExecuter().QueryAsync<TopicDto>(
                            @"SELECT d.draft_id,
				                     d.topic_id, 
				                     d.forum_id,
				                     d.draft_subject as topic_title,
				                     d.save_time as topic_last_post_time,
				                     t.topic_last_post_id
			                    FROM forum.phpbb_drafts d
			                    LEFT JOIN forum.phpbb_topics t
			                      ON d.topic_id = t.topic_id
		                       WHERE d.forum_id NOT IN @restrictedForumList
                                 AND d.user_id = @userId
                                 AND d.forum_id <> 0
                                 AND (t.topic_id IS NOT NULL OR d.topic_id = 0)
                               ORDER BY d.save_time DESC
                               LIMIT @skip, @take",
                            new
                            {
                                user.UserId,
                                skip = (PageNum - 1) * Constants.DEFAULT_PAGE_SIZE,
                                take = Constants.DEFAULT_PAGE_SIZE,
                                restrictedForumList
                            }
                        );
                        var countTask = Context.GetSqlExecuter().ExecuteScalarAsync<int>(
                            @"SELECT COUNT(*) as total_count
                                FROM forum.phpbb_drafts d
	                            LEFT JOIN forum.phpbb_topics t
	                              ON d.topic_id = t.topic_id
	                           WHERE d.forum_id NOT IN @restrictedForumList
                                 AND d.user_id = @user_id
	                             AND d.forum_id <> 0
	                             AND (t.topic_id IS NOT NULL OR d.topic_id = 0);",
                            new
                            {
                                user.UserId,
                                restrictedForumList
                            });

                        await Task.WhenAll(draftsTask, countTask);

                        Topics = (await draftsTask).AsList();
                        Paginator = new Paginator(count: await countTask, pageNum: PageNum, link: "/Drafts?pageNum=1", topicId: null);
                    },
                    evaluateSuccess: () => Topics!.Count > 0 && PageNum == Paginator!.CurrentPage,
                    fix: () => PageNum = Paginator!.CurrentPage);

                return Page();
            });

        public async Task<IActionResult> OnPostDeleteDrafts()
            => await WithRegisteredUser(async (_) =>
            {
                var sqlExecuter = Context.GetSqlExecuter();
                await sqlExecuter.ExecuteAsync(
                    "DELETE FROM phpbb_drafts WHERE draft_id IN @ids", 
                    new { ids = SelectedDrafts.DefaultIfNullOrEmpty() });
                return await OnGet();
            });
    }
}
