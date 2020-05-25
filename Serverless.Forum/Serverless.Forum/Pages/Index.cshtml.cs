using Serverless.Forum.Contracts;
using Serverless.Forum.ForumDb;
using Serverless.Forum.Services;
using Serverless.Forum.Utilities;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Serverless.Forum.Pages
{
    public class IndexModel : ModelWithLoggedUser
    {
        public List<ForumDisplay> Forums { get; private set; }

        public IndexModel(ForumDbContext context, ForumTreeService forumService, UserService userService, CacheService cacheService)
            : base(context, forumService, userService, cacheService)
        {
        }

        public async Task OnGet()
        {
            Forums = (await GetForumTreeAsync()).ChildrenForums.Where(f => f.ForumType == ForumType.Category).ToList();
        }
    }
}
