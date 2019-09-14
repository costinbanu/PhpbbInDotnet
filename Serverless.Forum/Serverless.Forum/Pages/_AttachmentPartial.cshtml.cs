using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Serverless.Forum.forum;
using Serverless.Forum.Utilities;
using System.Linq;

namespace Serverless.Forum.Pages
{
    public class _AttachmentPartialModel : ViewTopicModel
    {
        public bool IsRenderedInline = false;
        public bool IsDisplayedInline = false;
        public int Id;
        public string FileName;
        public string MimeType;

        public _AttachmentPartialModel(forumContext context, IHttpContextAccessor httpContext, ICompositeViewEngine viewEngine, IHtmlHelper<ViewTopicModel> html)
             : base(context, httpContext, viewEngine, html)
        {
        }

        public void OnGet()
        {
        }
    }
}