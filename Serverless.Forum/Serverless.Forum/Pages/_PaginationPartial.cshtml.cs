using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Serverless.Forum.Pages
{
    public class _PaginationPartialModel : PageModel
    {
        public void OnGet()
        {

        }

        public string Link;
        public int Posts;
        public int PostsPerPage;
    }
}