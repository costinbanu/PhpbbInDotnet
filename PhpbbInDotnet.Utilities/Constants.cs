using System;

namespace PhpbbInDotnet.Utilities
{
    public static class Constants
    {
        public const string REPLY = "Re: ";

        public const string SMILEY_PATH = "./images/smilies";

        public const int ANONYMOUS_USER_ID = 1;

        public const int GUESTS_GROUP_ID = 1;

        public const int BOTS_GROUP_ID = 6;

        public const int NO_PM_ROLE = 8;

        public const int OTHER_REPORT_REASON_ID = 4;

        public const int ACCESS_TO_FORUM_DENIED_ROLE = 16;

        public const int ADMIN_GROUP_ID = 5;

        public const int PAGE_SIZE_INCREMENT = 7;

        public const int DEFAULT_PAGE_SIZE = PAGE_SIZE_INCREMENT * 2;

        public static readonly TimeSpan REGEX_TIMEOUT = TimeSpan.FromSeconds(20);

        public const string DEFAULT_LANGUAGE = "en";

        public const string FORUM_CHECK_OVERRIDE_CACHE_KEY_FORMAT = "ForumCheckOverride_{0}";
    }
}
