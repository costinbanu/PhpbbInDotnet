using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Serverless.Forum.Pages.CustomPartials.Email
{
    public class _NewUserNotificationModel : PageModel
    {
        public string Username { get; }
       
        public _NewUserNotificationModel (string username)
        {
            Username = username;
        }
    }
}