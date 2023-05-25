using SixLabors.ImageSharp;
using System.IO;
using System.Threading.Tasks;

namespace PhpbbInDotnet.Services
{
	public interface IImageResizeService
	{
		Task<Stream?> ResizeImageByFileSize(Stream input, string fileName, double newSizeInBytes);
		Task<Stream?> ResizeImageByResolution(Image input, string fileName, int longestSideInPixels);
	}
}