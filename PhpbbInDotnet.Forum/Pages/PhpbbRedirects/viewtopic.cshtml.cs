using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PhpbbInDotnet.Database;
using PhpbbInDotnet.Database.Entities;
using System.Threading.Tasks;

namespace PhpbbInDotnet.Forum.Pages.PhpbbRedirects
{
    public class viewtopicModel : PageModel
    {
        private readonly IForumDbContext _context;

        public viewtopicModel(IForumDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> OnGet(int? f, int? t, int? p, int? start)
        {
            if (t.HasValue)
            {
                if (start.HasValue)
                {
                    var sqlExecuter = _context.GetSqlExecuter();
                    var post = await sqlExecuter.QueryFirstOrDefaultAsync<PhpbbPosts>(
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
                        return NotFound();
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
                return BadRequest();
            }
        }
    }
}