using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Serverless.Forum.Contracts;
using Serverless.Forum.forum;
using Serverless.Forum.Utilities;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;

namespace Serverless.Forum.Pages
{
    public class IndexModel : ModelWithLoggedUser
    {
        public List<ForumDisplay> Forums { get; private set; }

        public IndexModel(IConfiguration config, Utils utils) : base(config, utils)
        {
        }

        public async Task OnGet()
        {
            using (var context = new forumContext(_config))
            {
                var usr = await GetCurrentUser();
                Forums = await (from f1 in context.PhpbbForums
                                where f1.ForumType == 0
                                   && usr.UserPermissions != null
                                   && !usr.UserPermissions.Any(fp => fp.ForumId == f1.ForumId && fp.AuthRoleId == 16)
                                let firstChildren = from f2 in context.PhpbbForums
                                                    where f2.ParentId == f1.ForumId
                                                    orderby f2.LeftId

                                                    join u in context.PhpbbUsers
                                                    on f2.ForumLastPosterId equals u.UserId
                                                    into joined

                                                    from j in joined.DefaultIfEmpty()
                                                    select new ForumDisplay()
                                                    {
                                                        Id = f2.ForumId,
                                                        Name = HttpUtility.HtmlDecode(f2.ForumName),
                                                        LastPosterName = f2.ForumLastPosterName,
                                                        LastPosterId = j.UserId == 1 ? null as int? : j.UserId,
                                                        LastPostTime = f2.ForumLastPostTime.TimestampToLocalTime()
                                                    }
                                orderby f1.LeftId
                                select new ForumDisplay()
                                {
                                    Name = HttpUtility.HtmlDecode(f1.ForumName),
                                    Children = firstChildren
                                }).ToListAsync();
            }
        }
    }
}
