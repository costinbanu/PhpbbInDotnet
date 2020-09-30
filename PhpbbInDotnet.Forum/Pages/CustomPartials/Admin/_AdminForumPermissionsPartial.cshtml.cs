using Microsoft.AspNetCore.Mvc.RazorPages;
using PhpbbInDotnet.DTOs;
using PhpbbInDotnet.Database.Entities;
using PhpbbInDotnet.Utilities;
using System;
using System.Collections.Generic;

namespace PhpbbInDotnet.Forum.Pages.CustomPartials.Admin
{
    public class _AdminForumPermissionsPartialModel : PageModel
    {
        public PhpbbForums Forum { get; private set; }
        public IEnumerable<PhpbbForums> ForumChildren { get; private set; }
        public IEnumerable<ForumPermissions> Permissions { get; private set; }
        public AclEntityType EntityType { get; private set; }
        public readonly string Self;
        public readonly int StartIndex;

        public _AdminForumPermissionsPartialModel(
            PhpbbForums forum, IEnumerable<PhpbbForums> forumChildren, IEnumerable<ForumPermissions> forumPermissions, AclEntityType entityType)
        {
            Forum = forum;
            ForumChildren = forumChildren;
            Permissions = forumPermissions;
            EntityType = entityType;
            Self = Guid.NewGuid().ToString("n");
            StartIndex = 0;
        }
    }
}