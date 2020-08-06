using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Serverless.Forum.Contracts;
using Serverless.Forum.ForumDb.Entities;
using System.Collections.Generic;

namespace Serverless.Forum.Pages.CustomPartials
{
    public class _ForumTreePartialModel : PageModel
    {
        public HashSet<ForumTree> Tree { get; }
        public int? ForumId { get; }
        public int? TopicId { get; }
        public bool ConstrainSize { get; }
        public IEnumerable<MiniTopicDto> TopicData { get; }

        public _ForumTreePartialModel(HashSet<ForumTree> tree, IEnumerable<MiniTopicDto> topicData = null, int? forumId = null, int? topicId = null, bool constrainSize = false)
        {
            Tree = tree;
            ForumId = forumId;
            TopicId = topicId;
            ConstrainSize = constrainSize;
            TopicData = topicData;
        }
    }
}