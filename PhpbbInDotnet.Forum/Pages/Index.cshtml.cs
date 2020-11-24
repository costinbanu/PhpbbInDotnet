using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using PhpbbInDotnet.Objects;
using PhpbbInDotnet.Database;
using PhpbbInDotnet.Services;
using PhpbbInDotnet.Utilities;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PhpbbInDotnet.Forum.Pages
{
    public class IndexModel : ModelWithLoggedUser
    {
        private bool _forceTreeRefresh = false;

        public HashSet<ForumTree> Tree { get; private set; }

        public IndexModel(ForumDbContext context, ForumTreeService forumService, UserService userService, CacheService cacheService, IConfiguration config, 
            AnonymousSessionCounter sessionCounter, CommonUtils utils)
            : base(context, forumService, userService, cacheService, config, sessionCounter, utils)
        {
        }

        public async Task<IActionResult> OnGet()
        {
            (Tree, _) = await GetForumTree(_forceTreeRefresh, true);
            return Page();
        }

        public async Task<IActionResult> OnPostMarkForumsRead()
            => await WithRegisteredUser(async (_) => 
            {
                await MarkForumAndSubforumsRead(0);
                _forceTreeRefresh = true;
                return await OnGet();
            });
    }
}
