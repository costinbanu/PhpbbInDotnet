using Microsoft.AspNetCore.Mvc.RazorPages;
using PhpbbInDotnet.Objects;
using PhpbbInDotnet.Database.Entities;
using PhpbbInDotnet.Domain;
using System;
using System.Collections.Generic;

namespace PhpbbInDotnet.Forum.Pages.CustomPartials.Admin
{
    public class _AdminForumPermissionsPartialModel : PageModel
    {
        public PhpbbForums? Forum { get; set; }
        public IEnumerable<PhpbbForums>? ForumChildren { get; set; }
        public IEnumerable<ForumPermissions>? Permissions { get; set; }
        public AclEntityType EntityType { get; set; }
        public string Language { get; set; } = Constants.DEFAULT_LANGUAGE;
        public string Self { get; }

        public _AdminForumPermissionsPartialModel()
        {
            Self = Guid.NewGuid().ToString("n");
        }
    }
}