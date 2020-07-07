using Microsoft.AspNetCore.Mvc;
using Serverless.Forum.Contracts;
using Serverless.Forum.ForumDb;
using Serverless.Forum.Services;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Serverless.Forum.Pages
{
    public class IndexModel : ModelWithLoggedUser
    {
        private bool _forceTreeRefresh = false;

        public HashSet<ForumTree> Tree { get; private set; }

        public HashSet<Tracking> Tracking { get; private set; }

        public IndexModel(ForumDbContext context, ForumTreeService forumService, UserService userService, CacheService cacheService)
            : base(context, forumService, userService, cacheService)
        {
        }

        public async Task<IActionResult> OnGet()
        {
            (Tree, Tracking) = await GetForumTree(_forceTreeRefresh);
            return Page();
        }

        public async Task<IActionResult> OnPostMarkForumsRead()
            => await WithRegisteredUser(async () => 
            {
                await MarkForumAndSubforumsRead(0);
                _forceTreeRefresh = true;
                return await OnGet();
            });
    }
}
