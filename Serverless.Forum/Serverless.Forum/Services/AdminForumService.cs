using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Serverless.Forum.ForumDb;
using Serverless.Forum.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;

namespace Serverless.Forum.Services
{
    public class AdminForumService
    {
        private readonly IConfiguration _config;
        private readonly Utils _utils;
        private readonly ForumTreeService _forumService;

        public AdminForumService(IConfiguration config, Utils utils, ForumTreeService forumService)
        {
            _config = config;
            _utils = utils;
            _forumService = forumService;
        }

        public async Task<(string Message, bool? IsSuccess)> ManageForumsAsync(List<int> childrenForums, int forumId, string forumName, string forumDesc)
        {
            using (var context = new ForumDbContext(_config))
            {
                var children = await (
                    from f in context.PhpbbForums
                    where f.ParentId == forumId
                    orderby f.LeftId
                    select f
                ).ToListAsync();

                if (!children.Select(s => s.ForumId).SequenceEqual(childrenForums))
                {
                    children.ForEach(c => c.LeftId = (childrenForums.IndexOf(c.ForumId) + 1) * 2);
                }

                var actual = await context.PhpbbForums.FirstOrDefaultAsync(f => f.ForumId == forumId);
                if (string.IsNullOrWhiteSpace(forumName))
                {
                    return ("Numele forumului nu este valid!", false);
                }

                actual.ForumName = forumName;
                actual.ForumDesc = forumDesc;

                await context.SaveChangesAsync();

                return ($"Forumul {forumName} a fost actualizat cu succes!", true);
            }
        }

        public async Task<(PhpbbForums Forum, List<PhpbbForums> Children)> ShowForum(int forumId)
        {
            using (var context = new ForumDbContext(_config))
            {
                return (
                    await context.PhpbbForums.FirstOrDefaultAsync(f => f.ForumId == forumId),
                    await (
                        from f in context.PhpbbForums
                        where f.ParentId == forumId
                        orderby f.LeftId
                        select f
                    ).ToListAsync()
                );
            }
        }

        public async Task<List<SelectListItem>> FlatForumTreeAsListItem(int parentId, int forumId)
            => _forumService.GetPathInTree(
                await _forumService.GetForumTreeAsync(), 
                forum => new SelectListItem(forum.Name, forum.Id.ToString(), forum.Id == parentId, forum.Id == parentId || forum.Id == forumId || forum.ParentId == forumId),
                (item, level) => item.Text = $"{new string('-', level)} {item.Text}"
            );
    }
}
