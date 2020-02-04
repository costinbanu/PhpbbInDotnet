using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Serverless.Forum.Pages.CustomPartials.Email
{
    public class _WelcomeEmailPartialModel : PageModel
    {
        public string Subject { get; set; }
        public string RegistrationCode { get; set; }
        public string UserName { get; set; }
    }
}