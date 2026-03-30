using System;

namespace PhpbbInDotnet.Objects.Configuration;

public class RateLimitOptions
{
    public bool ShouldRateLimit { get; set; }
    public TimeSpan RequestTimeWindow { get; set; }
    public int RequestThresholdForRegisteredUsers { get; set; }
    public int RequestThresholdForOtherUsers { get; set; }
}
