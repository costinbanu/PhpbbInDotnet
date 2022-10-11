﻿using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PhpbbInDotnet.Database;
using PhpbbInDotnet.Database.Entities;
using PhpbbInDotnet.Domain.Utilities;
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