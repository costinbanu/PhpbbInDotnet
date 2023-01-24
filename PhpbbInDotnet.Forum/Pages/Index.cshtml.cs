using Microsoft.AspNetCore.Mvc;
using PhpbbInDotnet.Forum.Models;
using PhpbbInDotnet.Objects;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PhpbbInDotnet.Forum.Pages
{
    public class IndexModel : AuthenticatedPageModel
    {
        private bool _forceTreeRefresh = false;

        public HashSet<ForumTree>? Tree { get; private set; }

        public IndexModel(IServiceProvider serviceProvider) : base(serviceProvider)
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
