﻿using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Serverless.Forum.ForumDb;
using Serverless.Forum.Services;
using Serverless.Forum.Utilities;
using System.Linq;
using System.Threading.Tasks;

namespace Serverless.Forum.Pages.PhpbbRedirects
{
    public class viewtopicModel : ModelWithLoggedUser
    {
        public viewtopicModel(Utils utils, ForumDbContext context, ForumTreeService forumService, UserService userService, CacheService cacheService)
            : base(utils, context, forumService, userService, cacheService)
        {
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
                    return RedirectToPage("../ViewTopic", "ByPostId", new { PostId = posts[start.Value] });
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