using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Serverless.Forum.Contracts;
using System.Collections.Generic;

namespace Serverless.Forum.Pages.CustomPartials
{
    //[BindProperties(SupportsGet = true), ValidateAntiForgeryToken]
    public class _ForumTreePartialModel : PageModel
    {
        public ForumDisplay Forums { get; set; }
        public List<int> PathToForumOrTopic { get; set; }
        public int? ForumId { get; set; }
        public int? TopicId { get; set; }
    }
}