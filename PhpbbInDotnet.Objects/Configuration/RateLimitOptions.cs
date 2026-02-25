using System;

namespace PhpbbInDotnet.Objects.Configuration;

public class RateLimitOptions
{
    public bool ShouldRateLimit { get; set; }
    public TimeSpan TimeWindow { get; set; }
    public int Threshold { get; set; }
}
