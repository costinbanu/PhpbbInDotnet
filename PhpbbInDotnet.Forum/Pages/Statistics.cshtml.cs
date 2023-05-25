using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using PhpbbInDotnet.Database;
using PhpbbInDotnet.Domain;
using PhpbbInDotnet.Forum.Models;
using PhpbbInDotnet.Languages;
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

        public StatisticsModel(IForumTreeService forumService, IUserService userService, ISqlExecuter sqlExecuter, 
            ITranslationProvider translationProvider, IStatisticsService statisticsService, IConfiguration configuration)
            : base(forumService, userService, sqlExecuter, translationProvider, configuration)
        {
            _statisticsService = statisticsService;
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
