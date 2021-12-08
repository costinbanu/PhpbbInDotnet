﻿using Microsoft.AspNetCore.Mvc.RazorPages;
using PhpbbInDotnet.Database.Entities;
using PhpbbInDotnet.Utilities;
using System.Collections.Generic;

namespace PhpbbInDotnet.Forum.Pages.CustomPartials.Admin
{
    public class _AdminUsersSummaryPartialModel : PageModel
    {
        public string? DateFormat { get; set; }

        public List<PhpbbUsers>? Users { get; set; }

        public string Language { get; set; } = Constants.DEFAULT_LANGUAGE;

    }
}