using PhpbbInDotnet.Utilities;
using RandomTestValues;
using System;

namespace PhpbbInDotnet.Services.UnitTests.Utils
{
    internal static class CustomRandomValue
    {
        internal static long UnixTimeStamp()
            => RandomValue.DateTime(Extensions.UNIX_TIMESTAMP_START_DATE, DateTime.UtcNow).ToUnixTimestamp();
    }
}
