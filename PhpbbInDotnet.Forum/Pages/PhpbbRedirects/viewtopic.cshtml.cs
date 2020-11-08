using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using PhpbbInDotnet.Database;
using System.Linq;
using System.Threading.Tasks;

namespace PhpbbInDotnet.Forum.Pages.PhpbbRedirects
{
    public class viewtopicModel : PageModel
    {
        private readonly ForumDbContext _context;

        public viewtopicModel(ForumDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> OnGet(int? f, int? t, int? p, int? start)
        {
            if (f.HasValue && t.HasValue)
            {
                if (start.HasValue)
                {
                    var posts = await (from post in _context.PhpbbPosts.AsNoTracking()
                                       where post.TopicId == t.Value
                                       select post.PostId).ToListAsync();
                    if (start.Value < posts.Count)
                    {
                        return RedirectToPage("../ViewTopic", "ByPostId", new { PostId = posts[start.Value] });
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
                return BadRequest("Parametri greșiți!");
            }
        }
    }
}