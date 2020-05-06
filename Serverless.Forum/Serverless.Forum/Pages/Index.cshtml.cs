using Serverless.Forum.Contracts;
using Serverless.Forum.ForumDb;
using Serverless.Forum.Services;
using Serverless.Forum.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Serverless.Forum.Pages
{
    public class IndexModel : ModelWithLoggedUser
    {
        public List<ForumDisplay> Forums { get; private set; }

        public IndexModel(Utils utils, ForumDbContext context, ForumTreeService forumService, UserService userService, CacheService cacheService)
            : base(utils, context, forumService, userService, cacheService)
        {
        }

        public async Task OnGet()
        {
            //var r = new Random();
            //var exceptions1 = Enumerable.Repeat(new Exception(), 4).ToList();
            //exceptions1.Add(new Exception("this is a random one"));
            //var exceptions2 = Enumerable.Repeat(new Exception(), 4);
            //exceptions1.Add(new AggregateException(exceptions2));
            //var ex = new Exception("first", new AggregateException(exceptions1.OrderBy(a => Guid.NewGuid())));
            //throw ex;
            Forums = (await GetForumTreeAsync()).ChildrenForums.Where(f => f.ForumType == ForumType.Category).ToList();
        }
    }
}
