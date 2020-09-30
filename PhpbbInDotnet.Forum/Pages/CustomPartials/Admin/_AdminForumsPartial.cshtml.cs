using Microsoft.AspNetCore.Mvc.RazorPages;
using PhpbbInDotnet.DTOs;
using PhpbbInDotnet.Database.Entities;
using System.Collections.Generic;

namespace PhpbbInDotnet.Forum.Pages.CustomPartials.Admin
{
    public class _AdminForumsPartialModel : PageModel
    {
        public PhpbbForums Forum { get; private set; }
        public IEnumerable<PhpbbForums> ForumChildren { get; private set; }
        public IEnumerable<ForumPermissions> Permissions { get; private set; }
        public bool Show { get; set; }
        public LoggedUser CurrentUser { get; }

        public _AdminForumsPartialModel(PhpbbForums forum, IEnumerable<PhpbbForums> forumChildren, IEnumerable<ForumPermissions> forumPermissions, LoggedUser user, bool show)
        {
            Forum = forum;
            ForumChildren = forumChildren;
            Permissions = forumPermissions;
            Show = show;
            CurrentUser = user;
        }
    }
}