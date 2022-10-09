using Microsoft.Extensions.Configuration;
using PhpbbInDotnet.Domain.Extensions;
using PhpbbInDotnet.Objects.Configuration;
using PhpbbInDotnet.Services;

namespace PhpbbInDotnet.RecurringTasks
{
    class SchedulingService : ISchedulingService
    {
        public const string OK_FILE_NAME = $"{nameof(SchedulingService)}.ok";

        readonly ITimeService _timeService;
        readonly IFileInfoService _fileInfoService;
        readonly CleanupServiceOptions _options;

        public SchedulingService(ITimeService timeService, IFileInfoService fileInfoService, IConfiguration configuration)
        {
            _timeService = timeService;
            _fileInfoService = fileInfoService;
            _options = configuration.GetObject<CleanupServiceOptions>("CleanupService");
        }

        public TimeSpan GetTimeToWaitUntilRunIsAllowed()
        {
            var now = _timeService.DateTimeOffsetNow();
            if (_options.MinimumAllowedRunTime.Date != now.Date || _options.MaximumAllowedRunTime.Date != now.Date)
            {
                throw new ArgumentOutOfRangeException(
                    paramName: nameof(_options),
                    message: $"The {nameof(_options.MinimumAllowedRunTime)} and {nameof(_options.MaximumAllowedRunTime)} properties of {nameof(CleanupServiceOptions)} should not have a date component.");
            }
            if (_options.MinimumAllowedRunTime >= _options.MaximumAllowedRunTime)
            {
                throw new ArgumentOutOfRangeException(
                    paramName: nameof(_options),
                    message: $"The {nameof(_options.MinimumAllowedRunTime)} and {nameof(_options.MaximumAllowedRunTime)} properties of {nameof(CleanupServiceOptions)} should be correctly ordered.");
            }

            var timeUntilAllowedTimeFrame = GetTimeUntilAllowedRunTimeFrame();
            var timeSinceLastRun = GetElapsedTimeSinceLastRunIfAny();
            if (!timeSinceLastRun.HasValue || (timeSinceLastRun.Value + timeUntilAllowedTimeFrame >= _options.Interval))
            {
                return timeUntilAllowedTimeFrame;
            }
            else
            {
                var toReturn = timeUntilAllowedTimeFrame;
                while (timeSinceLastRun.Value + toReturn < _options.Interval)
                {
                    toReturn += TimeSpan.FromDays(1);
                }
                return toReturn;
            }

            TimeSpan? GetElapsedTimeSinceLastRunIfAny()
            {
                var lastRun = _fileInfoService.GetLastWriteTime(OK_FILE_NAME);
                return lastRun.HasValue ? now.DateTime.ToUniversalTime() - lastRun.Value : null;
            }

            TimeSpan GetTimeUntilAllowedRunTimeFrame()
            {
                if (now < _options.MinimumAllowedRunTime)
                {
                    return _options.MinimumAllowedRunTime - now;
                }
                else if (now > _options.MaximumAllowedRunTime)
                {
                    return _options.MinimumAllowedRunTime + TimeSpan.FromDays(1) - now;
                }
                return TimeSpan.Zero;
            }
        }

    }
}
