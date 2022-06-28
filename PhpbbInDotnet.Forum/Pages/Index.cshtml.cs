using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using PhpbbInDotnet.Objects;
using PhpbbInDotnet.Database;
using PhpbbInDotnet.Services;
using PhpbbInDotnet.Domain;
using System.Collections.Generic;
using System.Threading.Tasks;
using PhpbbInDotnet.Languages;
using LazyCache;

namespace PhpbbInDotnet.Forum.Pages
{
    public class IndexModel : AuthenticatedPageModel
    {
        private bool _forceTreeRefresh = false;

        public HashSet<ForumTree>? Tree { get; private set; }

        public IndexModel(IForumDbContext context, IForumTreeService forumService, IUserService userService, IAppCache cache, ICommonUtils utils, ITranslationProvider translationProvider)
            : base(context, forumService, userService, cache, utils, translationProvider)
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
