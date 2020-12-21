using Microsoft.AspNetCore.Mvc.RazorPages;

namespace PhpbbInDotnet.Forum.Pages.CustomPartials.Email
{
    public class _NewUserNotificationModel
    {
        public string Username { get; }
       
        public _NewUserNotificationModel (string username)
        {
            Username = username;
        }
    }
}