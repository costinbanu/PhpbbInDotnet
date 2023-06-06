using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PhpbbInDotnet.Database.Entities;
using PhpbbInDotnet.Database.SqlExecuter;
using PhpbbInDotnet.Domain.Utilities;
using System.Threading.Tasks;

namespace PhpbbInDotnet.Forum.Pages.PhpbbRedirects
{
    public class viewtopicModel : PageModel
    {
        private readonly ISqlExecuter _sqlExecuter;

        public viewtopicModel(ISqlExecuter sqlExecuter)
        {
            _sqlExecuter = sqlExecuter;
        }

        public async Task<IActionResult> OnGet(int? f, int? t, int? p, int? start)
        {
            if (t.HasValue)
            {
                if (start.HasValue)
                {
                    var post = await _sqlExecuter.WithPagination(start >= 1 ? start.Value - 1 : 0, 1).QueryFirstOrDefaultAsync<PhpbbPosts>(
                        "SELECT * FROM phpbb_posts WHERE topic_id = @topicId ORDER BY post_time",
                        new
                        {
                            topicId = t.Value,
                        }
                    );

                    if (post != null)
                    {
                        var redirect = ForumLinkUtility.GetRedirectObjectToPost(post.PostId);
                        return RedirectToPage(redirect.Url, redirect.RouteValues);
                    }
                    else
                    {
                        return NotFound();
                    }
                }
                else
                {
                    var redirect = ForumLinkUtility.GetRedirectObjectToTopic(topicId: t.Value, pageNum: 1);
                    return RedirectToPage(redirect.Url, redirect.RouteValues);
                }
            }
            else if (p.HasValue)
            {
                var redirect = ForumLinkUtility.GetRedirectObjectToPost(postId: p.Value);
                return RedirectToPage(redirect.Url, redirect.RouteValues);
            }
            else
            {
                return BadRequest();
            }
        }
    }
}