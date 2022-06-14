using PhpbbInDotnet.Utilities.Extensions;
using RandomTestValues;
using System;

namespace PhpbbInDotnet.Services.UnitTests.Utils
{
    internal static class CustomRandomValue
    {
        internal static long UnixTimeStamp()
            => RandomValue.DateTime(UnixTimeStampExtensions.UNIX_TIMESTAMP_START_DATE, DateTime.UtcNow).ToUnixTimestamp();
    }
}
