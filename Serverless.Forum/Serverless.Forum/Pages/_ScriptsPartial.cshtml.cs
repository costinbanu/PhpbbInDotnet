using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Serverless.Forum.forum;
using Serverless.Forum.Utilities;

namespace Serverless.Forum.Pages
{
    public class _ScriptsPartialModel : PageModel
    {
        forumContext _dbContext;

        public _ScriptsPartialModel(forumContext dbContext)
        {
            _dbContext = dbContext;
        }

        public List<string> BbCodes { get; set; }
        public Dictionary<string, string> BbCodeHelpLines { get; set; } 

        public void OnGet()
        {
            var customBbCodes = (from b in _dbContext.PhpbbBbcodes
                                 where b.DisplayOnPosting == 1
                                 select b).ToList();

            BbCodes = new List<string>(Constants.BBCODES);
            foreach (var bbCode in customBbCodes)
            {
                BbCodes.Add(bbCode.BbcodeTag);
                BbCodes.Add($"[/{bbCode.BbcodeTag.Substring(2)}");
            }

            BbCodeHelpLines = new Dictionary<string, string>(Constants.BBCODE_HELPLINES);
            foreach (var bbCode in customBbCodes)
            {
                var index = BbCodes.IndexOf(bbCode.BbcodeTag);
                BbCodeHelpLines.Add($"cb_{index}", bbCode.BbcodeHelpline);aaaaaa
            }
        }
}