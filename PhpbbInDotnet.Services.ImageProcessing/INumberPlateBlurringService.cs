using Microsoft.AspNetCore.Http;

namespace PhpbbInDotnet.Services.ImageProcessing;

public interface INumberPlateBlurringService
{
    Task<IEnumerable<IFormFile>> BlurNumberPlates(IEnumerable<IFormFile> images, CancellationToken cancellationToken = default);
}
