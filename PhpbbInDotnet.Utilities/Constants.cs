using System;

namespace PhpbbInDotnet.Utilities
{
    public static class Constants
    {
        public static readonly string REPLY = "Re: ";

        public static readonly string SMILEY_PATH = "./images/smilies";

        public static readonly int ANONYMOUS_USER_ID = 1;

        public static readonly int NO_PM_ROLE = 8;

        public static readonly int OTHER_REPORT_REASON_ID = 4;

        public static readonly int ACCESS_TO_FORUM_DENIED_ROLE = 16;

        public static readonly int ADMIN_GROUP_ID = 5;

        public static readonly int PAGE_SIZE_INCREMENT = 7;

        public static readonly int DEFAULT_PAGE_SIZE = PAGE_SIZE_INCREMENT * 2;

        public static readonly int BOTS_GROUP_ID = 6;

        public static readonly TimeSpan REGEX_TIMEOUT = TimeSpan.FromSeconds(20);

        public static readonly string DEFAULT_LANGUAGE = "ro";

        public static readonly string FORUM_CHECK_OVERRIDE_CACHE_KEY_FORMAT = "ForumCheckOverride_{0}";
    }
}
