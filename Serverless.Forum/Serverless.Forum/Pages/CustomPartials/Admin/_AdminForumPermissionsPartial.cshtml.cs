using Microsoft.AspNetCore.Mvc.RazorPages;
using Serverless.Forum.Contracts;
using Serverless.Forum.ForumDb.Entities;
using Serverless.Forum.Utilities;
using System;
using System.Collections.Generic;

namespace Serverless.Forum.Pages.CustomPartials.Admin
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