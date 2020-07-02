using Microsoft.AspNetCore.Mvc.RazorPages;
using Serverless.Forum.Contracts;
using Serverless.Forum.ForumDb;
using Serverless.Forum.Utilities;
using System.Collections.Generic;
using System.Linq;

namespace Serverless.Forum.Pages.CustomPartials
{
    public class _ForumDisplayPartialModel : PageModel
    {
        public IEnumerable<PhpbbForums> Categories { get; }
        public IEnumerable<PhpbbForums> SubForums { get; }
        public string DateFormat { get; }
        public bool ShowTitle { get; }
        public ForumDto Forum { get; }
        public LoggedUser LoggedUser { get; }

        public _ForumDisplayPartialModel(ForumDto forum, string dateFormat, bool showTitle, LoggedUser loggedUser)
        {
            Categories = forum.ChildrenForums.Where(f => f.ForumType == ForumType.Category).OrderBy(c => c.LeftId);
            SubForums = forum.ChildrenForums.Where(f => f.ForumType == ForumType.SubForum).OrderBy(f => f.LeftId);
            DateFormat = dateFormat;
            ShowTitle = showTitle;
            Forum = forum;
            LoggedUser = loggedUser;
        }
    }
}