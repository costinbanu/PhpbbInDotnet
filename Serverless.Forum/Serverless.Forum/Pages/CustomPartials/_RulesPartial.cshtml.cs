﻿using Microsoft.AspNetCore.Mvc.RazorPages;
using Serverless.Forum.Services;
using Serverless.Forum.Utilities;
using System.Web;

namespace Serverless.Forum.Pages.CustomPartials
{
    public class _RulesPartialModel : PageModel
    {
        public _RulesPartialModel(BBCodeRenderingService renderingService, Utils utils, string forumRules, string forumRulesUid, string forumRulesLink)
        {
            if (!string.IsNullOrWhiteSpace(forumRules))
            {
                ForumRules = renderingService.BbCodeToHtml(forumRules, forumRulesUid ?? string.Empty);
            }

            if (!string.IsNullOrWhiteSpace(forumRulesLink))
            {
                ForumRulesLink = utils.TransformSelfLinkToBetaLink(HttpUtility.HtmlDecode(forumRulesLink));
            }
        }

        public string ForumRules { get; }
        public string ForumRulesLink { get; }
    }
}