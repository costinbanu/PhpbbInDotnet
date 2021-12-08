using Microsoft.AspNetCore.Mvc.RazorPages;
using PhpbbInDotnet.Database.Entities;
using PhpbbInDotnet.Objects;
using PhpbbInDotnet.Utilities;
using System.Collections.Generic;

namespace PhpbbInDotnet.Forum.Pages.CustomPartials.Admin
{
    public class _AdminWritingPartialModel : PageModel
    {
        public string? DateFormat { get; set; }
        public string Language { get; set; } = Constants.DEFAULT_LANGUAGE;
        public List<PhpbbWords>? BannedWords { get; set; }
        public List<PhpbbBbcodes>? CustomBbCodes { get; set; }
        public List<PhpbbSmilies>? Smilies { get; set; }
    }
}