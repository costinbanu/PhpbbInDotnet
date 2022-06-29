using System;

namespace PhpbbInDotnet.Domain.Extensions
{
    public static class UnixTimeStampExtensions
    {
        public static readonly DateTime UNIX_TIMESTAMP_START_DATE = new(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);

        public static DateTime ToUtcTime(this long timestamp)
            => UNIX_TIMESTAMP_START_DATE.AddSeconds(timestamp);

        public static long ToUnixTimestamp(this DateTime time)
            => (long)time.ToUniversalTime().Subtract(UNIX_TIMESTAMP_START_DATE).TotalSeconds;
    }
}
