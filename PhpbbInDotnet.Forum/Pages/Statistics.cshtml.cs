using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PhpbbInDotnet.Domain;
using PhpbbInDotnet.Objects;
using PhpbbInDotnet.Services;
using System;
using System.Threading.Tasks;

namespace PhpbbInDotnet.Forum.Pages
{
    public class StatisticsModel : PageModel
    {
        [BindProperty]
        public StatisticsPeriod? Period { get; set; }

        public TimedStatistics? Result { get; private set; }

        private readonly IStatisticsService _statisticsService;

        public StatisticsModel(IStatisticsService statisticsService)
        {
            _statisticsService = statisticsService;
        }

        public void OnGet()
        {

        }

        public async Task OnPost()
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
                    _ => throw new NotImplementedException($"Unknown value '{Period}' for {nameof(Period)}.")
                };

                Result = await _statisticsService.GetTimedStatistics(startTime);
            }
        }
    }
}
