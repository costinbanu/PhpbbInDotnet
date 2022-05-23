using Dapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using PhpbbInDotnet.Database;
using PhpbbInDotnet.Database.Entities;
using PhpbbInDotnet.Utilities;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace PhpbbInDotnet.Forum.Pages.PhpbbRedirects
{
    public class viewtopicModel : PageModel
    {
        private readonly IForumDbContext _context;
        private readonly CommonUtils _utils;

        public viewtopicModel(IForumDbContext context, CommonUtils utils)
        {
            _context = context;
            _utils = utils;
        }

        public async Task<IActionResult> OnGet(int? f, int? t, int? p, int? start)
        {
            if (t.HasValue)
            {
                if (start.HasValue)
                {
                    var conn = _context.GetDbConnection();
                    var post = await conn.QueryFirstOrDefaultAsync<PhpbbPosts>(
                        "SELECT * FROM phpbb_posts WHERE topic_id = @topicId ORDER BY post_time LIMIT @skip, 1",
                        new
                        {
                            topicId = t.Value,
                            skip = start >= 1 ? start - 1 : 0
                        }
                    );

                    if (post != null)
                    {
                        return RedirectToPage("../ViewTopic", "ByPostId", new { post.PostId });
                    }
                    else
                    {
                        return RedirectToPage("../ViewTopic", new { TopicId = t.Value, PageNum = 1 });
                    }
                }
                else
                {
                    return RedirectToPage("../ViewTopic", new { TopicId = t.Value, PageNum = 1 });
                }
            }
            else if (p.HasValue)
            {
                return RedirectToPage("../ViewTopic", "ByPostId", new { PostId = p.Value });
            }
            else
            {
                _utils.HandleErrorAsWarning(new Exception($"Bad request to legacy viewtopic.php route: {Request.QueryString.Value}"));
                return RedirectToPage("../Index");
            }
        }
    }
}