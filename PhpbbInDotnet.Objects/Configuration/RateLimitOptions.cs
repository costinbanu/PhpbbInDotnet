using System;

namespace PhpbbInDotnet.Objects.Configuration;

public class RateLimitOptions
{
    public bool ShouldRateLimit { get; set; }
    public TimeSpan RequestTimeWindow { get; set; }
    public TimeSpan ClientTimeWindow { get; set; }
    public int RequestThreshold { get; set; }
    public int ClientThreshold { get; set; }
}
