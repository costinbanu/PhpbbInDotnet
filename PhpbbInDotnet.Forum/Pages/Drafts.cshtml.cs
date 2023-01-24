using Dapper;
using Microsoft.AspNetCore.Mvc;
using PhpbbInDotnet.Domain;
using PhpbbInDotnet.Domain.Extensions;
using PhpbbInDotnet.Domain.Utilities;
using PhpbbInDotnet.Forum.Models;
using PhpbbInDotnet.Objects;
using System;
using System.Collections.Generic;
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

        public DraftsModel(IServiceProvider serviceProvider) : base(serviceProvider)
        {
        }

        public async Task<IActionResult> OnGet()
            => await WithRegisteredUser(async (user) =>
            {
                await ResiliencyUtility.RetryOnceAsync(
                    toDo: async () =>
                    {
                        var restrictedForumList = await GetRestrictedForums();
                        PageNum = Paginator.NormalizePageNumberLowerBound(PageNum);
                        var draftsTask = SqlExecuter.QueryAsync<TopicDto>(
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
                        var countTask = SqlExecuter.ExecuteScalarAsync<int>(
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
                await SqlExecuter.ExecuteAsync(
                    "DELETE FROM phpbb_drafts WHERE draft_id IN @ids", 
                    new { ids = SelectedDrafts.DefaultIfNullOrEmpty() });
                return await OnGet();
            });
    }
}
