using System;

namespace PhpbbInDotnet.Services
{
    class TimeService : ITimeService
    {
        public DateTimeOffset DateTimeOffsetNow()
            => DateTimeOffset.Now;
    }
}
