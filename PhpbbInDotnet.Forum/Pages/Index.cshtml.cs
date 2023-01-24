using LazyCache;
using Microsoft.AspNetCore.Mvc;
using PhpbbInDotnet.Database;
using PhpbbInDotnet.Forum.Models;
using PhpbbInDotnet.Languages;
using PhpbbInDotnet.Objects;
using PhpbbInDotnet.Services;
using Serilog;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PhpbbInDotnet.Forum.Pages
{
    public class IndexModel : AuthenticatedPageModel
    {
        private bool _forceTreeRefresh = false;

        public HashSet<ForumTree>? Tree { get; private set; }

        public IndexModel(IForumDbContext context, IForumTreeService forumService, IUserService userService, IAppCache cache, ILogger logger, ITranslationProvider translationProvider)
            : base(context, forumService, userService, cache, logger, translationProvider)
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
