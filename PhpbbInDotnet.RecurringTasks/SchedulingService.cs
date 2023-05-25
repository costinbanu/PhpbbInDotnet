using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PhpbbInDotnet.Domain.Extensions;
using PhpbbInDotnet.Objects.Configuration;
using PhpbbInDotnet.Services;
using PhpbbInDotnet.Services.Storage;
using System;
using System.Threading.Tasks;

namespace PhpbbInDotnet.RecurringTasks
{
    class SchedulingService : ISchedulingService
    {
        readonly ITimeService _timeService;
		readonly IServiceProvider _serviceProvider;
		readonly CleanupServiceOptions _options;

        static readonly TimeSpan AllowedDiff = TimeSpan.FromMinutes(5);

        public SchedulingService(ITimeService timeService, IConfiguration configuration, IServiceProvider serviceProvider)
        {
            _timeService = timeService;
            _serviceProvider = serviceProvider;
            _options = configuration.GetObject<CleanupServiceOptions>("CleanupService");
        }

        public async Task<TimeSpan> GetTimeToWaitUntilRunIsAllowed()
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

            var timeUntilAllowedTimeFrame = GetTimeUntilAllowedRunTimeFrame(now);
            var timeSinceLastRun = await GetElapsedTimeSinceLastRunIfAny(now);
            if (!timeSinceLastRun.HasValue || ((timeSinceLastRun.Value + timeUntilAllowedTimeFrame - _options.Interval).Duration() <= AllowedDiff))
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
        }

        async Task<TimeSpan?> GetElapsedTimeSinceLastRunIfAny(DateTimeOffset now)
        {
            using var scope = _serviceProvider.CreateScope();
            var storageService = scope.ServiceProvider.GetRequiredService<IStorageService>();
            var lastRun = await storageService.GetLastWriteTime(Orchestrator.ControlFileName);
            return lastRun.HasValue ? now.DateTime.ToUniversalTime() - lastRun.Value : null;
        }

        TimeSpan GetTimeUntilAllowedRunTimeFrame(DateTimeOffset now)
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
