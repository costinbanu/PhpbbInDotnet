using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using PhpbbInDotnet.Database.Entities;
using PhpbbInDotnet.Objects;
using PhpbbInDotnet.Domain;
using System.Collections.Generic;

namespace PhpbbInDotnet.Forum.Pages.CustomPartials.Admin
{
    public class _AdminUsersPartialModel : PageModel
    {
        public string? DateFormat { get; set; }

        public List<PhpbbUsers>? SearchResults { get; set; }
        public List<PhpbbUsers>? InactiveUsers { get; set; }
        public List<UpsertGroupDto>? Groups { get; set; }
        public List<PhpbbRanks>? Ranks { get; set; }
        public List<UpsertBanListDto>? BanList { get; set; }
        public List<SelectListItem>? RankListItems { get; set; }
        public List<SelectListItem>? RoleListItems { get; set; }

        public AdminUserSearch? SearchParameters { get; set; }

        public string Language { get; set; } = Constants.DEFAULT_LANGUAGE;

        public bool WasSearchPerformed { get; set; } = false;

    }
}