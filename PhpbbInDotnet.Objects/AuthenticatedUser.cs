using PhpbbInDotnet.Domain;

namespace PhpbbInDotnet.Objects
{
    public class AuthenticatedUser
    {
        public int UserId { get; set; }

        public string? Username { get; set; } = null;

        public string? UsernameClean { get; set; } = null;

        public string? UserDateFormat { get; set; } = null;

        public string? UserColor { get; set; } = null;

        public bool IsAnonymous => UserId == Constants.ANONYMOUS_USER_ID;

        public bool AllowPM { get; set; }

        public bool? JumpToUnread { get; set; } = true;

        public string Language { get; set; } = Constants.DEFAULT_LANGUAGE;

        public string? EmailAddress { get; set; }

        public bool ShouldConfirmEmail { get; set; }
    }
}
