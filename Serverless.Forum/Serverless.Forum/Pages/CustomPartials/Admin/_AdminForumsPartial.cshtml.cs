using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Serverless.Forum.Contracts;
using Serverless.Forum.ForumDb;
using Serverless.Forum.Utilities;

namespace Serverless.Forum.Pages.CustomPartials.Admin
{
    public class _AdminForumsPartialModel : PageModel
    {
        private readonly IConfiguration _config;
        private readonly Utils _utils;

        public ForumDisplay ForumTree { get; }
        public List<int> PathInTree { get; private set; }
        [BindProperty]
        public PhpbbForums Forum { get; set; }
        public int SelectedForumId { get; private set; }
        public List<PhpbbForums> Children { get; private set; }

        public _AdminForumsPartialModel(IConfiguration config, Utils utils, ForumDisplay forumTree, List<int> pathInTree)
        {
            _config = config;
            _utils = utils;
            ForumTree = forumTree;
            PathInTree = pathInTree;
        }

        public async Task ManageForumsAsync(int forumId, int[] newLeftIds, PhpbbForums changedForum)
        {
            changedforum e aproape default, nu este trimis!!!
            using (var context = new ForumDbContext(_config))
            {
                var children = await (
                    from f in context.PhpbbForums
                    where f.ParentId == forumId
                    orderby f.LeftId
                    select f
                ).ToListAsync();

                if (!children.Select(s => s.LeftId).SequenceEqual(newLeftIds))
                {
                    for (int i = 0; i < children.Count; i++)
                    {
                        children[i].LeftId = newLeftIds[i];
                    }
                }
            }
        }

        public async Task ShowForum(int forumId, Func<int, Task<List<int>>> getPath)
        {
            using (var context = new ForumDbContext(_config))
            {
                Forum = await context.PhpbbForums.FirstOrDefaultAsync(f => f.ForumId == forumId);
                SelectedForumId = forumId;
                PathInTree = await getPath(forumId);
                Children = await (
                    from f in context.PhpbbForums
                    where f.ParentId == Forum.ForumId
                    orderby f.LeftId
                    select f
                ).ToListAsync();
            }
        }
    }
}