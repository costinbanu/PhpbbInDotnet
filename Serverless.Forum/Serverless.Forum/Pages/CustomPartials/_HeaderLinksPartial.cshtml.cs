using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Serverless.Forum.Pages.CustomPartials
{
    public class _HeaderLinksPartialModel : PageModel
    {
        public int? ParentForumId { get; }
        public string ParentForumTitle { get; }
        public _HeaderLinksPartialModel(int parentForumId, string parentForumTitle)
        {
            ParentForumId = parentForumId;
            ParentForumTitle = parentForumTitle;
        }

        public _HeaderLinksPartialModel()
        {
            ParentForumId = null;
            ParentForumTitle = null;
        }
    }
}