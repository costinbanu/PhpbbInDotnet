using Microsoft.AspNetCore.Mvc.RazorPages;
using PhpbbInDotnet.Forum.Contracts;
using PhpbbInDotnet.Forum.Utilities;
using System.Collections.Generic;
using System.Linq;

namespace PhpbbInDotnet.Forum.Pages.CustomPartials
{
    public class _ForumDisplayPartialModel : PageModel
    {
        public string DateFormat { get; }
        public bool ShowTitle { get; }
        public LoggedUser LoggedUser { get; }
        public HashSet<ForumTree> Tree { get; }
        public IEnumerable<ForumTree> Categories { get; }
        public IEnumerable<ForumTree> SubForums { get; }
        public bool ShowLastSeparator { get; }

        public _ForumDisplayPartialModel(int forumId, HashSet<ForumTree> tree, string dateFormat, bool showTitle, LoggedUser loggedUser, bool showLastSeparator)
        {
            DateFormat = dateFormat;
            ShowTitle = showTitle;
            LoggedUser = loggedUser;
            Tree = tree;
            Categories = GetChildrenForums(forumId).Where(f => f.ForumType == ForumType.Category);
            SubForums = GetChildrenForums(forumId).Where(f => f.ForumType == ForumType.SubForum);
            ShowLastSeparator = showLastSeparator;
        }

        public IEnumerable<ForumTree> GetChildrenForums(int forumId)
            => (GetForum(forumId)?.ChildrenList ?? new HashSet<int>()).Select(GetForum);

        private ForumTree GetForum(int forumId)
        {
            if (Tree != null && Tree.TryGetValue(new ForumTree { ForumId = forumId }, out var forum))
            {
                return forum;
            }
            return null;
        }
    }
}