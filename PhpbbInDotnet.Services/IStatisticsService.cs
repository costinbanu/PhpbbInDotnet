﻿using PhpbbInDotnet.Objects;
using System;
using System.Threading.Tasks;

namespace PhpbbInDotnet.Services
{
    public interface IStatisticsService
    {
        Task<Statistics> GetStatisticsSummary();
        Task<TimedStatistics> GetTimedStatistics(DateTime? startTime);
    }
}