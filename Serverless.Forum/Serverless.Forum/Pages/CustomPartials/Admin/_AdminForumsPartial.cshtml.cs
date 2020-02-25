using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Serverless.Forum.Contracts;
using Serverless.Forum.ForumDb;
using Serverless.Forum.Utilities;

namespace Serverless.Forum.Pages.CustomPartials.Admin
{
    public class _AdminForumsPartialModel : PageModel
    {
        public PhpbbForums Forum { get; private set; }
        public IEnumerable<PhpbbForums> ForumChildren { get; private set; }
        public IEnumerable<ForumPermissions> Permissions { get; private set; }
        public bool Show { get; set; }

        public _AdminForumsPartialModel(PhpbbForums forum, IEnumerable<PhpbbForums> forumChildren, IEnumerable<ForumPermissions> forumPermissions, bool show)
        {
            Forum = forum;
            ForumChildren = forumChildren;
            Permissions = forumPermissions;
            Show = show;
        }
    }
}