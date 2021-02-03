﻿using Microsoft.AspNetCore.Mvc.RazorPages;
using PhpbbInDotnet.Database.Entities;
using PhpbbInDotnet.Objects;
using System.Collections.Generic;

namespace PhpbbInDotnet.Forum.Pages.CustomPartials.Admin
{
    public class _AdminUsersPartialModel : PageModel
    {
        public string DateFormat { get; set; }

        public List<PhpbbUsers> SearchResults { get; set; }

        public AdminUserSearch SearchParameters { get; set; }

        public string Language { get; set; }

        public bool WasSearchPerformed { get; set; } = false;

    }
}