using Microsoft.AspNetCore.Mvc.RazorPages;
using PhpbbInDotnet.Objects;
using PhpbbInDotnet.Utilities;
using System.Collections.Generic;
using System.Linq;

namespace PhpbbInDotnet.Forum.Pages.CustomPartials
{
    public class _ForumDisplayPartialModel : PageModel
    {
        public string DateFormat { get; }
        public bool ShowTitle { get; }
        public AuthenticatedUserExpanded AuthenticatedUser { get; }
        public HashSet<ForumTree> Tree { get; }
        public IEnumerable<ForumTree> Categories { get; }
        public IEnumerable<ForumTree> SubForums { get; }
        public bool ShowLastSeparator { get; }
        public string Language { get; set; } = Constants.DEFAULT_LANGUAGE;

        public _ForumDisplayPartialModel(int forumId, HashSet<ForumTree> tree, string dateFormat, bool showTitle, AuthenticatedUserExpanded authenticatedUser, bool showLastSeparator, string language)
        {
            DateFormat = dateFormat;
            ShowTitle = showTitle;
            AuthenticatedUser = authenticatedUser;
            Tree = tree;
            Categories = GetChildrenForums(forumId).Where(f => f.ForumType == ForumType.Category);
            SubForums = GetChildrenForums(forumId).Where(f => f.ForumType == ForumType.SubForum);
            ShowLastSeparator = showLastSeparator;
            Language = language;
        }

        public IEnumerable<ForumTree> GetChildrenForums(int forumId)
            => (GetForum(forumId)?.ChildrenList ?? new HashSet<int>()).Select(GetForum).Where(ft => ft is not null).Cast<ForumTree>();

        private ForumTree? GetForum(int forumId)
        {
            if (Tree != null && Tree.TryGetValue(new ForumTree { ForumId = forumId }, out var forum))
            {
                return forum;
            }
            return null;
        }
    }
}