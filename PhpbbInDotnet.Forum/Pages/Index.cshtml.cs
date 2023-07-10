using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using PhpbbInDotnet.Database.SqlExecuter;
using PhpbbInDotnet.Forum.Models;
using PhpbbInDotnet.Languages;
using PhpbbInDotnet.Objects;
using PhpbbInDotnet.Services;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PhpbbInDotnet.Forum.Pages
{
    public class IndexModel : AuthenticatedPageModel
    {
        private bool _forceTreeRefresh = false;

        public HashSet<ForumTree>? Tree { get; private set; }

        public IndexModel(IForumTreeService forumService, IUserService userService, ISqlExecuter sqlExecuter, 
            ITranslationProvider translationProvider, IConfiguration configuration)
            : base(forumService, userService, sqlExecuter, translationProvider, configuration)
        {
        }

        public async Task<IActionResult> OnGet()
        {
            Tree = await ForumService.GetForumTree(ForumUser, _forceTreeRefresh, true);
            return Page();
        }

        public async Task<IActionResult> OnPostMarkForumsRead()
            => await WithRegisteredUser(async (_) => 
            {
                await ForumService.MarkForumAndSubforumsRead(ForumUser, 0);
                _forceTreeRefresh = true;
                return await OnGet();
            });
    }
}
