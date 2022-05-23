using System;

namespace PhpbbInDotnet.Services
{
    public interface ITimeService
    {
        DateTimeOffset DateTimeOffsetNow();
    }
}