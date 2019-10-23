using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Configuration;
using Serverless.Forum.Utilities;

namespace Serverless.Forum.Pages
{
    public class _SummaryPartialModel : PageModel
    {
        public int? AuthorId { get; set; }
        public string AuthorName { get; set; }
        public DateTime? CreationTime { get; set; }
        public int AssetId { get; set; }
        public string DateFormat { get; set; }

        public void OnGet()
        {

        }
    }
}