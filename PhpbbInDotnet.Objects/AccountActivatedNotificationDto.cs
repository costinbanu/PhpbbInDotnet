using PhpbbInDotnet.Utilities;

namespace PhpbbInDotnet.Objects
{
    public class AccountActivatedNotificationDto
    {
        public string? Username { get; set; }

        public string Language { get; set; } = Constants.DEFAULT_LANGUAGE;
    }
}
