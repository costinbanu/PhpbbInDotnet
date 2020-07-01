using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Serverless.Forum.Contracts;
using System.Collections.Generic;

namespace Serverless.Forum.Pages.CustomPartials
{
    public class _ForumTreePartialModel : PageModel
    {
        public ForumDto Forums { get; }
        public IEnumerable<int> Path { get; }
        public int? ForumId { get; }
        public int? TopicId { get; }
        public bool ConstrainSize { get; }

        public _ForumTreePartialModel(ForumDto forums, IEnumerable<int> path = null, int? forumId = null, int? topicId = null, bool constrainSize = false)
        {
            Forums = forums;
            Path = path;
            ForumId = forumId;
            TopicId = topicId;
            ConstrainSize = constrainSize;
        }
    }
}