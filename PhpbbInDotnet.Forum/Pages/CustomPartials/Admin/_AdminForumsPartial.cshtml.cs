using Microsoft.AspNetCore.Mvc.RazorPages;
using PhpbbInDotnet.Objects;
using PhpbbInDotnet.Database.Entities;
using System.Collections.Generic;
using PhpbbInDotnet.Domain;

namespace PhpbbInDotnet.Forum.Pages.CustomPartials.Admin
{
    public class _AdminForumsPartialModel : PageModel
    {
        public PhpbbForums? Forum { get; set; }
        public int? ParentId { get; set; }
        public IEnumerable<PhpbbForums>? ForumChildren { get; set; }
        public IEnumerable<ForumPermissions>? Permissions { get; set; }
        public bool Show { get; set; }
        public ForumUserExpanded? CurrentUser { get; set; }
        public string Language { get; set; } = Constants.DEFAULT_LANGUAGE;
        public bool IsRoot { get; set; }
    }
}