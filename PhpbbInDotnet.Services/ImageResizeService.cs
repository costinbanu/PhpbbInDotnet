using ImageMagick;
using PhpbbInDotnet.Objects;
using System;
using System.IO;
using System.Threading.Tasks;

namespace PhpbbInDotnet.Services
{
    class ImageResizeService : IImageResizeService
    {
        public async Task<ImageResizeResult?> ResizeImageByFileSize(Stream input, string fileName, double newSizeInBytes)
        {
            using var image = new MagickImageCollection();
            await image.ReadAsync(input);

            double originalSize = input.Length;
            Stream? result = null;
            uint width = 0, height = 0;

            for (var count = 0; originalSize > newSizeInBytes && count < 5; count++)
            {
                var scale = Math.Sqrt(newSizeInBytes / originalSize) * (count == 0 ? 1d : 0.9 / count);
                if (scale > 0 && scale < 1)
                {
                    result = new MemoryStream();
                    (width, height) = await ResizeImageCore(scale, fileName, image, result);
                    originalSize = result.Length;
                }
            }

            return result is null ? null : new(result, width, height);
        }

		public async Task<ImageResizeResult?> ResizeImageByResolution(Stream input, string fileName, int longestSideInPixels)
		{
			// Cheap header-only read, no full decode
			var info = new MagickImageInfo(input);
			var scale = (double)longestSideInPixels / Math.Max(info.Width, info.Height);
			if (scale <= 0 || scale >= 1)
			{
				return null;
			}

			input.Seek(0, SeekOrigin.Begin);
			using var images = new MagickImageCollection();
			await images.ReadAsync(input);

			var result = new MemoryStream();
			var (width, height) = await ResizeImageCore(scale, fileName, images, result);
			return new(result, width, height);
		}

        static async Task<(uint width, uint height)> ResizeImageCore(double scale, string fileName, MagickImageCollection images, Stream result)
        {
            var format = images[0].Format;
            if (format == MagickFormat.Unknown)
            {
                throw new InvalidOperationException($"Unknown image format in file '{fileName}'.");
            }

            if (images.Count > 1)
            {
                images.Coalesce(); // so animated GIF frames resize correctly
            }

            foreach (var frame in images)
            {
                frame.Resize((uint)(frame.Width * scale), (uint)(frame.Height * scale));
            }

            await images.WriteAsync(result, format);
            result.Seek(0, SeekOrigin.Begin);
            return (images[0].Width, images[0].Height);
        }

        public async Task<(uint width, uint height)> GetImageSize(Stream input)
        {
            input.Seek(0, SeekOrigin.Begin);
            using var images = new MagickImageCollection();
			await images.ReadAsync(input);
            input.Seek(0, SeekOrigin.Begin);
            return (images[0].Width, images[0].Height);
        }
    }
}