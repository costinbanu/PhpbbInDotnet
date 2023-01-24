using Dapper;
using Microsoft.AspNetCore.Mvc;
using PhpbbInDotnet.Domain;
using PhpbbInDotnet.Domain.Extensions;
using PhpbbInDotnet.Forum.Models;
using PhpbbInDotnet.Objects;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;

namespace PhpbbInDotnet.Forum.Pages
{
    [ValidateAntiForgeryToken]
    public class NewPostsModel : AuthenticatedPageModel
    {
        private bool _forceTreeRefresh;

        public List<TopicDto> Topics { get; private set; } = new List<TopicDto>();
        public Paginator? Paginator { get; private set; }

        [BindProperty(SupportsGet = true)]
        public int PageNum { get; set; } = 1;

        [BindProperty]
        public string[]? SelectedNewPosts { get; set; }

        public NewPostsModel(IServiceProvider serviceProvider) : base(serviceProvider)
        {
        }

        public async Task<IActionResult> OnGet()
            => await WithRegisteredUser(async (user) =>
            {
                var tree = await GetForumTree(_forceTreeRefresh, true);
                var restrictedForumList = await GetRestrictedForums();
                var topicList = tree.Tracking!.Where(ft => !restrictedForumList.Contains(ft.Key)).SelectMany(t => t.Value).Select(t => t.TopicId).Distinct();
                Paginator = new Paginator(count: topicList.Count(), pageNum: PageNum, link: "/NewPosts?pageNum=1", topicId: null);
                PageNum = Paginator.CurrentPage;

                Topics = (await SqlExecuter.QueryAsync<TopicDto>(
                    @"SELECT t.topic_id, 
	                         t.forum_id,
	                         t.topic_title, 
                             count(p.post_id) AS post_count,
                             t.topic_views AS view_count,
                             t.topic_last_poster_id,
                             t.topic_last_poster_name,
                             t.topic_last_post_time,
                             t.topic_last_poster_colour,
                             t.topic_last_post_id
                        FROM forum.phpbb_topics t
                        JOIN forum.phpbb_posts p ON t.topic_id = p.topic_id
                       WHERE t.topic_id IN @topicList
                         AND t.forum_id NOT IN @restrictedForumList
                       GROUP BY t.topic_id
                       ORDER BY t.topic_last_post_time DESC
                       LIMIT @skip, @take",
                    new
                    {
                        topicList = topicList.DefaultIfEmpty(),
                        skip = (PageNum - 1) * Constants.DEFAULT_PAGE_SIZE,
                        take = Constants.DEFAULT_PAGE_SIZE,
                        restrictedForumList
                    }
                )).AsList();

                return Page();
            });

        public async Task<IActionResult> OnPostMarkNewPostsRead()
            => await WithRegisteredUser(async (_) =>
            {
                foreach (var post in SelectedNewPosts ?? Enumerable.Empty<string>())
                {
                    var values = post?.Split(';');
                    if ((values?.Length ?? 0) != 2)
                    {
                        continue;
                    }
                    var forumId = int.TryParse(values![0], out var val) ? val : 0;
                    var topicId = int.TryParse(values[1], out val) ? val : 0;
                    await MarkTopicRead(forumId, topicId, true, DateTime.UtcNow.ToUnixTimestamp());
                }
                _forceTreeRefresh = true;
                return await OnGet();
            });
    }
}
