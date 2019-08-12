using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Serverless.Forum.Utilities
{
    public static class Extensions
    {
        public static DateTime TimestampToLocalTime(this int timestamp)
        {
            var toReturn = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
            toReturn = toReturn.AddSeconds(timestamp).ToLocalTime();
            return toReturn;
        }
    }
}
