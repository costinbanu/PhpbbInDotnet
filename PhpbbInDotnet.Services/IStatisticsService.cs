using PhpbbInDotnet.Objects;
using System.Threading.Tasks;

namespace PhpbbInDotnet.Services
{
    public interface IStatisticsService
    {
        int RefreshIntervalMinutes { get; }

        Task<Statistics> GetStatistics();
    }
}