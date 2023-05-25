using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using System;
using System.IO;
using System.Threading.Tasks;

namespace PhpbbInDotnet.Services
{
	class ImageResizeService : IImageResizeService
	{
		public async Task<Stream?> ResizeImageByFileSize(Stream input, string fileName, double newSizeInBytes)
		{
			using var image = await Image.LoadAsync(input);
			double originalSize = input.Length;
			Stream? result = null;
			for (var count = 0; originalSize > newSizeInBytes && count < 5; count++)
			{
				var scale = Math.Sqrt(newSizeInBytes / originalSize) * (count == 0 ? 1d : 0.9 / count);
				if (scale > 0 && scale < 1)
				{
					result = new MemoryStream();
					await ResizeImageCore(scale, fileName, image, result);
					originalSize = result.Length;
				}
			}
			return result;
		}

		public async Task<Stream?> ResizeImageByResolution(Image input, string fileName, int longestSideInPixels)
		{
			var scale = (double)longestSideInPixels / Math.Max(input.Width, input.Height);
			if (scale > 0 && scale < 1)
			{
				var result = new MemoryStream();
				await ResizeImageCore(scale, fileName, input, result); 
				return result;
			}
			return null;
		}

		static async Task ResizeImageCore(double scale, string fileName, Image image, Stream result)
		{
			image.Mutate(ctx => ctx.Resize((int)(image.Width * scale), (int)(image.Height * scale)));
			await image.SaveAsync(result, image.Metadata.DecodedImageFormat ?? throw new InvalidOperationException($"Unknown image format in file '{fileName}'."));
			result.Seek(0, SeekOrigin.Begin);
		}
	}
}
