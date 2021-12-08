using PhpbbInDotnet.Utilities;

namespace PhpbbInDotnet.Forum.Pages.CustomPartials.Email
{
    public class _NewUserNotificationModel
    {
        public string? Username { get; set;  }

        public string Language { get; set; } = Constants.DEFAULT_LANGUAGE;
       
    }
}