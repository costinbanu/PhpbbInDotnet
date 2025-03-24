using Dapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using PhpbbInDotnet.Database.SqlExecuter;
using PhpbbInDotnet.Domain;
using PhpbbInDotnet.Domain.Extensions;
using PhpbbInDotnet.Forum.Models;
using PhpbbInDotnet.Languages;
using PhpbbInDotnet.Objects;
using PhpbbInDotnet.Services;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;

namespace PhpbbInDotnet.Forum.Pages
{
    [ValidateAntiForgeryToken, ResponseCache(NoStore = true, Duration = 0)]
    public class NewPostsModel : AuthenticatedPageModel
    {
        private bool _forceTreeRefresh;

        public List<TopicDto> Topics { get; private set; } = new List<TopicDto>();
        public Paginator? Paginator { get; private set; }

        [BindProperty(SupportsGet = true)]
        public int PageNum { get; set; } = 1;

        [BindProperty]
        public string[]? SelectedNewPosts { get; set; }

        public NewPostsModel(IForumTreeService forumService, IUserService userService, ISqlExecuter sqlExecuter, 
            ITranslationProvider translationProvider, IConfiguration configuration)
            : base(forumService, userService, sqlExecuter, translationProvider, configuration)
        { }

        public async Task<IActionResult> OnGet()
            => await WithRegisteredUser(async (user) =>
            {
                var tracking = await ForumService.GetForumTracking(ForumUser.UserId, _forceTreeRefresh);
                var restrictedForumList = (await ForumService.GetRestrictedForumList(ForumUser)).Select(f => f.forumId).DefaultIfEmpty();
                var topicList = tracking.Where(ft => !restrictedForumList.Contains(ft.Key)).SelectMany(t => t.Value).Select(t => t.TopicId).Distinct();
                Paginator = new Paginator(count: topicList.Count(), pageNum: PageNum, link: "/NewPosts?pageNum=1", topicId: null);
                PageNum = Paginator.CurrentPage;

                Topics = (await SqlExecuter.CallStoredProcedureAsync<TopicDto>(
					"get_new_posts",
                    new
                    {
                        topicList = string.Join(",", topicList.DefaultIfEmpty()),
                        restrictedForumList = string.Join(",", restrictedForumList),
                        skip = (PageNum - 1) * Constants.DEFAULT_PAGE_SIZE,
                        take = Constants.DEFAULT_PAGE_SIZE
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
                    await ForumService.MarkTopicRead(ForumUser.UserId, forumId, topicId, true, DateTime.UtcNow.ToUnixTimestamp());
                }
                _forceTreeRefresh = true;
                return await OnGet();
            });
    }
}
