using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Newtonsoft.Json;
using Serverless.Forum.forum;
using Serverless.Forum.Utilities;

namespace Serverless.Forum.Pages
{
    public class _PostingScriptsPartialModel : PageModel
    {
        public string BbCodes { get; set; }
        public string BbCodeHelpLines { get; set; }

        public void OnGet()
        {

        }
    }
}