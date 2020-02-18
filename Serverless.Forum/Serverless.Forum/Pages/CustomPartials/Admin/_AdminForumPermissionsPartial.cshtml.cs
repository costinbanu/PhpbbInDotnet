using Microsoft.AspNetCore.Mvc.RazorPages;
using Serverless.Forum.Contracts;
using Serverless.Forum.ForumDb;
using Serverless.Forum.Utilities;
using System.Collections.Generic;
using System.Linq;

namespace Serverless.Forum.Pages.CustomPartials.Admin
{
    public class _AdminForumPermissionsPartialModel : PageModel
    {
        public PhpbbForums Forum { get; private set; }
        public IEnumerable<PhpbbForums> ForumChildren { get; private set; }
        public IEnumerable<ForumPermissions> Permissions { get; private set; }
        public Dictionary<int, int> PermissionsForAclEntity { get; set; }

        public _AdminForumPermissionsPartialModel(PhpbbForums forum, IEnumerable<PhpbbForums> forumChildren, IEnumerable<ForumPermissions> forumPermissions)
        {
            Forum = forum;
            ForumChildren = forumChildren;
            Permissions = forumPermissions;
            PermissionsForAclEntity = Permissions == null ? null : (
                from p in Permissions
                where p.HasRole
                select new { p.Id, p.RoleId }
            ).ToDictionary(x => x.Id, x => x.RoleId);
        }
    }
}