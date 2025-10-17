using SixLabors.ImageSharp;

namespace PhpbbInDotnet.Services.ImageProcessing;

public interface IImageSizeService
{
	Task<Stream?> ResizeImageByFileSize(Stream input, string fileName, double newSizeInBytes);
	Task<Stream?> ResizeImageByResolution(Image input, string fileName, int longestSideInPixels);
	(int Width, int Height) GetImageDimensions(Stream input);
}
