using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Serverless.Forum.forum;

namespace Serverless.Forum.Pages
{
    public class IndexModel : PageModel
    {
        forumContext _context;
        public IndexModel(forumContext context)
        {
            _context = context;
        }

        public void OnGet()
        {
            Forums = string.Join("<br/>", _context.PhpbbForums.Select(f => f.ForumName));
        }

        public string Forums;
    }
}
