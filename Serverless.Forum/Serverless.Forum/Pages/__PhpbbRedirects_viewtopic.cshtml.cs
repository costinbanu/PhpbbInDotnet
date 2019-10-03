using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Serverless.Forum.forum;
using Serverless.Forum.Utilities;
using System.Linq;

namespace Serverless.Forum.Pages
{
    public class __PhpbbRedirects_viewtopicModel : PageModel
    {
        private forumContext _dbContext;

        public __PhpbbRedirects_viewtopicModel(forumContext dbContext)
        {
            _dbContext = dbContext;
        }

        public IActionResult OnGet(int? f, int? t, int? p, int? start)
        {
            if (f.HasValue && t.HasValue)
            {
                if (start.HasValue)
                {
                    var posts = (from post in Utils.Instance.GetPosts(t.Value, _dbContext)
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