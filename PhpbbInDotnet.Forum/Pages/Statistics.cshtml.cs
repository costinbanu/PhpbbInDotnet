using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using PhpbbInDotnet.Domain;
using PhpbbInDotnet.Forum.Models;
using PhpbbInDotnet.Objects;
using PhpbbInDotnet.Services;
using System;
using System.Threading.Tasks;

namespace PhpbbInDotnet.Forum.Pages
{
    public class StatisticsModel : AuthenticatedPageModel
    {
        [BindProperty]
        public StatisticsPeriod? Period { get; set; }

        public TimedStatistics? Result { get; private set; }

        private readonly IStatisticsService _statisticsService;

        public StatisticsModel(IServiceProvider serviceProvider) : base(serviceProvider)
        {
            _statisticsService = serviceProvider.GetRequiredService<IStatisticsService>(); ;
        }

        public Task<IActionResult> OnGet()
            => WithRegisteredUser(_ => Task.FromResult<IActionResult>(Page()));

        public Task<IActionResult> OnPost()
            => WithRegisteredUser(async _ =>
            {
                if (Period is not null)
                {
                    var now = DateTime.UtcNow;
                    DateTime? startTime = Period switch
                    {
                        StatisticsPeriod.TwentyFourHours => now.AddHours(-24),
                        StatisticsPeriod.SevenDays => now.AddDays(-7),
                        StatisticsPeriod.ThirtyDays => now.AddDays(-30),
                        StatisticsPeriod.SixMonths => now.AddMonths(-6),
                        StatisticsPeriod.OneYear => now.AddYears(-1),
                        StatisticsPeriod.AllTime => null,
                        _ => throw new NotImplementedException($"Unknown value '{Period}' for {nameof(StatisticsPeriod)}.")
                    };

                    Result = await _statisticsService.GetTimedStatistics(startTime);
                }

                return Page();
            });
    }
}
