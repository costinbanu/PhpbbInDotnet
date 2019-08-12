using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Web;

namespace Serverless.Forum.Pages
{
    public class LogoutModel : PageModel
    {
        public IActionResult OnGet(string returnUrl)
        {
            HttpContext.Session.Remove("user");
            return Redirect(HttpUtility.UrlDecode(returnUrl));
        }
    }
}