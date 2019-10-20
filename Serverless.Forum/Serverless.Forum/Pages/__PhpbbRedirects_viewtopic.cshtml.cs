using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Serverless.Forum.Utilities;
using System.Linq;
using System.Threading.Tasks;

namespace Serverless.Forum.Pages
{
    public class __PhpbbRedirects_viewtopicModel : PageModel
    {
        private readonly Utils _utils;

        public __PhpbbRedirects_viewtopicModel(Utils utils)
        {
            _utils = utils;
        }

        public async Task<IActionResult> OnGet(int? f, int? t, int? p, int? start)
        {
            if (f.HasValue && t.HasValue)
            {
                if (start.HasValue)
                {
                    var posts = (from post in await _utils.GetPosts(t.Value)
                                 select post.PostId).ToList();
                    return RedirectToPage("ViewTopic", "ByPostId", new { PostId = posts[start.Value] });
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