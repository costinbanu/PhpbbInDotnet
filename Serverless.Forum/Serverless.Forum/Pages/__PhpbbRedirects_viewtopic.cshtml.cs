using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Serverless.Forum.ForumDb;
using Serverless.Forum.Utilities;
using System.Linq;
using System.Threading.Tasks;

namespace Serverless.Forum.Pages
{
    public class __PhpbbRedirects_viewtopicModel : ModelWithLoggedUser
    {
        public __PhpbbRedirects_viewtopicModel(IConfiguration config, Utils utils) : base(config, utils)
        {
        }

        public async Task<IActionResult> OnGet(int? f, int? t, int? p, int? start)
        {
            if (f.HasValue && t.HasValue)
            {
                if (start.HasValue)
                {
                    using (var context = new ForumDbContext(_config))
                    {
                        var posts = await (from post in context.PhpbbPosts
                                           where post.TopicId == t.Value
                                           select post.PostId).ToListAsync();
                        return RedirectToPage("ViewTopic", "ByPostId", new { PostId = posts[start.Value] });
                    }
                }
                else
                {
                    return RedirectToPage("ViewTopic", new { TopicId = t.Value });
                }
            }
            else if (p.HasValue)
            {
                return RedirectToPage("ViewTopic", "ByPostId", new { PostId = p.Value });
            }
            else
            {
                return BadRequest("Parametri greșiți!");
            }
        }
    }
}