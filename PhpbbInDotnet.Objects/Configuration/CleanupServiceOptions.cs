using System;

namespace PhpbbInDotnet.Objects.Configuration
{
    public class CleanupServiceOptions
    {
        public TimeSpan Interval { get; set; } = TimeSpan.FromDays(1);
        public DateTimeOffset MinimumAllowedRunTime { get; set; } = DateTimeOffset.Parse("02:00:00");
        public DateTimeOffset MaximumAllowedRunTime { get; set; } = DateTimeOffset.Parse("04:00:00");
    }
}
