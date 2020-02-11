using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Serverless.Forum.ForumDb;
using Serverless.Forum.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Serverless.Forum.Admin
{
    public class ForumService
    {
        private readonly IConfiguration _config;
        private readonly Utils _utils;

        public ForumService(IConfiguration config, Utils utils)
        {
            _config = config;
            _utils = utils;
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
    }
}
