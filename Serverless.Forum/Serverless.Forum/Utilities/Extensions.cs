using Microsoft.AspNetCore.Http;
using System;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;

namespace Serverless.Forum.Utilities
{
    public static class Extensions
    {
        static readonly DateTime DATE_SEED = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);

        public static DateTime ToUtcTime(this long timestamp)
            => DATE_SEED.AddSeconds(timestamp);

        public static long ToUnixTimestamp(this DateTime time)
            => (long)time.ToUniversalTime().Subtract(DATE_SEED).TotalSeconds;

        public static bool IsMimeTypeInline(this string mimeType)
            => mimeType.StartsWith("image", StringComparison.InvariantCultureIgnoreCase) /*||
                mimeType.StartsWith("video", StringComparison.InvariantCultureIgnoreCase) ||
                mimeType.EndsWith("pdf", StringComparison.InvariantCultureIgnoreCase)*/;

        public static T RunSync<T>(this Task<T> asyncTask)
            => asyncTask.GetAwaiter().GetResult();

        public static Bitmap ToImage(this IFormFile file)
        {
            if (file == null)
            {
                return null;
            }
            try
            {
                var stream = file.OpenReadStream();
                using var bmp = new Bitmap(stream);
                stream.Seek(0, SeekOrigin.Begin);
                return bmp;
            }
            catch
            {
                return null;
            }
        }
    }
}
