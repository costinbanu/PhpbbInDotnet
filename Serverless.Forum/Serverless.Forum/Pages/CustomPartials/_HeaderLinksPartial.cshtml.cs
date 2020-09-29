using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Serverless.Forum.Pages.CustomPartials
{
    public class _HeaderLinksPartialModel : PageModel
    {
        public _HeaderLinksPartialModel(bool hasPrivateMessages, params string[] extraElements)
        {
            ExtraElements = extraElements;
            HasPrivateMessages = hasPrivateMessages;
        }

        public string[] ExtraElements { get; }
        public bool HasPrivateMessages { get; }
    }
}