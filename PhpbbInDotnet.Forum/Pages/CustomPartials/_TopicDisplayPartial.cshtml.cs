using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PhpbbInDotnet.Objects;
using System.Collections.Generic;

namespace PhpbbInDotnet.Forum.Pages.CustomPartials
{
    public class _TopicDisplayPartialModel : PageModel
    {
        public AuthenticatedUserExpanded CurrentUser { get; }
        public string Language { get; }
        public List<TopicGroup> Topics { get; }
        public int ForumId { get; }
        public bool AllowNewTopicCreation { get; set; }
        public bool AllowTopicSelection { get; set; }
        public int[]? SelectedTopicIds { get; set; }

        public _TopicDisplayPartialModel(AuthenticatedUserExpanded currentUser, string language, List<TopicGroup> topics, int forumId)
        {
            CurrentUser = currentUser;
            Language = language;
            Topics = topics;
            ForumId = forumId;
        }
    }
}
