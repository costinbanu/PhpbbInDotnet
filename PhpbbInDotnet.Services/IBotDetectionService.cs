using System.Threading.Tasks;

namespace PhpbbInDotnet.Services;

public interface IBotDetectionService
{
    Task<bool> IsBot(string? botDetectorResponse, string remoteIp);
}
