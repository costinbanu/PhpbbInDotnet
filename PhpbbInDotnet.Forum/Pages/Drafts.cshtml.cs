using Dapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using PhpbbInDotnet.Database.SqlExecuter;
using PhpbbInDotnet.Domain;
using PhpbbInDotnet.Domain.Extensions;
using PhpbbInDotnet.Domain.Utilities;
using PhpbbInDotnet.Forum.Models;
using PhpbbInDotnet.Languages;
using PhpbbInDotnet.Objects;
using PhpbbInDotnet.Services;
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

        public DraftsModel(IForumTreeService forumService, IUserService userService, ISqlExecuter sqlExecuter, 
            ITranslationProvider translationProvider, IConfiguration configuration)
            : base(forumService, userService, sqlExecuter, translationProvider, configuration)
        { }

        public async Task<IActionResult> OnGet()
            => await WithRegisteredUser(async (user) =>
            {
                await ResiliencyUtility.RetryOnceAsync(
                    toDo: async () =>
                    {
                        var restrictedForumList = (await ForumService.GetRestrictedForumList(ForumUser)).Select(f => f.forumId).DefaultIfEmpty();
                        PageNum = Paginator.NormalizePageNumberLowerBound(PageNum);
                        var draftsTask = SqlExecuter.WithPagination((PageNum - 1) * Constants.DEFAULT_PAGE_SIZE, Constants.DEFAULT_PAGE_SIZE).QueryAsync<TopicDto>(
                            @"SELECT d.draft_id,
				                     d.topic_id, 
				                     d.forum_id,
				                     d.draft_subject as topic_title,
				                     d.save_time as topic_last_post_time,
				                     t.topic_last_post_id
			                    FROM phpbb_drafts d
			                    LEFT JOIN phpbb_topics t
			                      ON d.topic_id = t.topic_id
		                       WHERE d.forum_id NOT IN @restrictedForumList
                                 AND d.user_id = @userId
                                 AND d.forum_id <> 0
                                 AND (t.topic_id IS NOT NULL OR d.topic_id = 0)
                               ORDER BY d.save_time DESC",
                            new
                            {
                                user.UserId,
                                restrictedForumList
                            }
                        );
                        var countTask = SqlExecuter.ExecuteScalarAsync<int>(
                            @"SELECT COUNT(*) as total_count
                                FROM phpbb_drafts d
	                            LEFT JOIN phpbb_topics t
	                              ON d.topic_id = t.topic_id
	                           WHERE d.forum_id NOT IN @restrictedForumList
                                 AND d.user_id = @userId
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
                await SqlExecuter.ExecuteAsync(
                    "DELETE FROM phpbb_drafts WHERE draft_id IN @ids", 
                    new { ids = SelectedDrafts.DefaultIfNullOrEmpty() });
                return await OnGet();
            });
    }
}
