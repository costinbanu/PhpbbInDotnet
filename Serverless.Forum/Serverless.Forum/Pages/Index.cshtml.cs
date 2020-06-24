using Serverless.Forum.Contracts;
using Serverless.Forum.ForumDb;
using Serverless.Forum.Services;
using Serverless.Forum.Utilities;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Serverless.Forum.Pages
{
    public class IndexModel : ModelWithLoggedUser
    {
        public ConcurrentBag<ForumDto> Forums { get; private set; }

        public IndexModel(ForumDbContext context, ForumTreeService forumService, UserService userService, CacheService cacheService)
            : base(context, forumService, userService, cacheService)
        {
        }

        public async Task OnGet()
        {
            var childrenIds = (await GetForumRoot()).ChildList;
            Forums = new ConcurrentBag<ForumDto>();

            Parallel.ForEach(childrenIds, async (childId) =>
            {
                var child = (await GetForumTree()).FirstOrDefault(f => f.ForumId == childId && f.ForumType == ForumType.Category);
                var grandChildren = (await GetForumTree()).Where(f => child.ChildList.Contains(f.ForumId));
                Forums.Add(new ForumDto
                {
                    Id = child.ForumId,
                    Description = child.ForumDesc
                })
            });todo find out best way to map this

            Forums = .ToList();
        }
    }
}
