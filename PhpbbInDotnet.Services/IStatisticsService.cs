using PhpbbInDotnet.Objects;
using System.Threading.Tasks;

namespace PhpbbInDotnet.Services
{
    public interface IStatisticsService
    {
        Task<Statistics> GetStatistics();
    }
}