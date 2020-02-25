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
        public Dictionary<AclEntityType,Dictionary<int, int>> RolesForAclEntity { get; set; }
        public AclEntityType EntityType { get; private set; }

        public _AdminForumPermissionsPartialModel(PhpbbForums forum, IEnumerable<PhpbbForums> forumChildren, IEnumerable<ForumPermissions> forumPermissions, AclEntityType entityType)
        {
            Forum = forum;
            ForumChildren = forumChildren;
            Permissions = forumPermissions;
            EntityType = entityType;
            if (Permissions != null)
            {
                RolesForAclEntity = new Dictionary<AclEntityType, Dictionary<int, int>>
                {
                    [EntityType] = (
                        from p in Permissions
                        where p.HasRole
                        select new { p.Id, p.RoleId }
                    ).ToDictionary(x => x.Id, x => x.RoleId)
                };
            }
        }
    }
}