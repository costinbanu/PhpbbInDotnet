using System.IO;
using System.Threading.Tasks;
using PhpbbInDotnet.Objects;

namespace PhpbbInDotnet.Services
{
	public interface IImageResizeService
	{
		Task<ImageResizeResult?> ResizeImageByFileSize(Stream input, string fileName, double newSizeInBytes);
		Task<ImageResizeResult?> ResizeImageByResolution(Stream input, string fileName, int longestSideInPixels);
		Task<(uint width, uint height)> GetImageSize(Stream input);
	}
}