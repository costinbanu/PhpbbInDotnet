using PhpbbInDotnet.Utilities.Extensions;
using System;

namespace PhpbbInDotnet.Utilities
{
    public static class Constants
    {
        public const string REPLY = "Re: ";

        public const int ANONYMOUS_USER_ID = 1;

        public const string ANONYMOUS_USER_NAME = "Anonymous";

        public const string DEFAULT_USER_COLOR = "000000";

        public const int GUESTS_GROUP_ID = 1;

        public const int BOTS_GROUP_ID = 6;

        public const int NO_PM_ROLE = 8;

        public const int FORUM_RESTRICTED_ROLE = 16;

        public const int FORUM_READONLY_ROLE = 17;

        public const int OTHER_REPORT_REASON_ID = 4;

        public const int ADMIN_GROUP_ID = 5;

        public const int PAGE_SIZE_INCREMENT = 7;

        public const int DEFAULT_PAGE_SIZE = PAGE_SIZE_INCREMENT * 2;

        public static readonly TimeSpan REGEX_TIMEOUT = TimeSpan.FromSeconds(20);

        public const string DEFAULT_LANGUAGE = "en";

        public static readonly DateTime UNIX_TIMESTAMP_MIN_VALUE = 0L.ToUtcTime();

        public const int ONE_MB = 1048576;

    }
}
