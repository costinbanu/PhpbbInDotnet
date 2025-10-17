using FastANPRDotNet;
using Microsoft.AspNetCore.Http;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Drawing;
using SixLabors.ImageSharp.Drawing.Processing;
using Path = System.IO.Path;

namespace PhpbbInDotnet.Services.ImageProcessing;

class NumberPlateBlurringService(INprDetectionService nprDetectionService) : INumberPlateBlurringService
{
    public async Task<IEnumerable<IFormFile>> BlurNumberPlates(IEnumerable<IFormFile> images, CancellationToken cancellationToken = default)
    {
        var filesOnDisk = (await Task.WhenAll(images.Select(async image =>
        {
            var tempFilePath = Path.GetTempFileName();
            using (var stream = File.Create(tempFilePath))
            {
                await image.CopyToAsync(stream);
            }
            return (tempFilePath, image);
        }))).ToDictionary();

        var results = await nprDetectionService.DetectNumberPlatesAsync(filesOnDisk.Keys.ToList(), cancellationToken);

        return await Task.WhenAll(results.Select(async result =>
        {
            var blurredStream = await BlurLicensePlates(result, cancellationToken);
            var originalFile = filesOnDisk[result.File];
            return new FormFile(blurredStream, 0, blurredStream.Length, originalFile.Name, originalFile.FileName)
            {
                Headers = originalFile.Headers,
                ContentType = originalFile.ContentType
            };
        }));
    }

    private static async Task<Stream> BlurLicensePlates(NumberPlateDetectionResult detectionResult, CancellationToken cancellationToken)
    {
        using var image = await Image.LoadAsync(detectionResult.File, cancellationToken);
        foreach (var plate in detectionResult.NumberPlates)
        {
            var poly = new Polygon(plate.RecognizedPolygon.Select(p => new PointF(p[0], p[1])).ToArray());
            image.Mutate(x => x.Clip(poly, y => y.GaussianBlur(15)));
        }

        var toReturn = new MemoryStream();
        await image.SaveAsync(toReturn, image.Metadata.DecodedImageFormat ?? throw new InvalidOperationException($"Unknown image format in file '{detectionResult.File}'."), cancellationToken);
        toReturn.Seek(0, SeekOrigin.Begin);
        return toReturn;
    }
}
