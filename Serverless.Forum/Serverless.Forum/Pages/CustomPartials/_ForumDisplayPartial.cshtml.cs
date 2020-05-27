using Microsoft.AspNetCore.Mvc.RazorPages;
using Serverless.Forum.Contracts;
using Serverless.Forum.Utilities;
using System.Collections.Generic;
using System.Linq;

namespace Serverless.Forum.Pages.CustomPartials
{
    public class _ForumDisplayPartialModel : PageModel
    {
        public IEnumerable<ForumDto> Categories { get; private set; }
        public IEnumerable<ForumDto> SubForums { get; private set; }
        public string DateFormat { get; private set; }
        public bool ShowTitle { get; private set; }

        public _ForumDisplayPartialModel(IEnumerable<ForumDto> forums, string dateFormat, bool showTitle)
        {
            Categories = forums.Where(f => f.ForumType == ForumType.Category);
            SubForums = forums.Where(f => f.ForumType == ForumType.SubForum);
            DateFormat = dateFormat;
            ShowTitle = showTitle;
        }
    }
}