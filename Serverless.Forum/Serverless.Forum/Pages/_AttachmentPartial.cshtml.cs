using Microsoft.AspNetCore.Mvc.ViewEngines;
using Serverless.Forum.forum;

namespace Serverless.Forum.Pages
{
    public class _AttachmentPartialModel : ViewTopicModel
    {
        public bool IsRenderedInline = false;
        public bool IsDisplayedInline = false;
        public int Id;
        public string FileName;
        public string MimeType;

        public _AttachmentPartialModel(forumContext context, ICompositeViewEngine viewEngine)
             : base(context, viewEngine)
        {
        }

        public void OnGet()
        {
        }
    }
}