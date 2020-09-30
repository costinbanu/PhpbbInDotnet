using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using PhpbbInDotnet.Forum.Contracts;
using PhpbbInDotnet.Forum.ForumDb;
using PhpbbInDotnet.Forum.Services;
using PhpbbInDotnet.Forum.Utilities;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PhpbbInDotnet.Forum.Pages
{
    public class IndexModel : ModelWithLoggedUser
    {
        private bool _forceTreeRefresh = false;

        public HashSet<ForumTree> Tree { get; private set; }

        public IndexModel(ForumDbContext context, ForumTreeService forumService, UserService userService, CacheService cacheService, IConfiguration config, 
            AnonymousSessionCounter sessionCounter, Utils utils)
            : base(context, forumService, userService, cacheService, config, sessionCounter, utils)
        {
        }

        public async Task<IActionResult> OnGet()
        {
            (Tree, _) = await GetForumTree(_forceTreeRefresh);
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
