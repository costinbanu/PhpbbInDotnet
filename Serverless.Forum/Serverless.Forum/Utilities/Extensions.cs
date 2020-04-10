using Microsoft.AspNetCore.Http;
using Serverless.Forum.ForumDb;
using Serverless.Forum.Pages.CustomPartials;
using System;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;

namespace Serverless.Forum.Utilities
{
    public static class Extensions
    {
        public static DateTime ToUtcTime(this long timestamp)
        {
            var seed = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
            return seed.AddSeconds(timestamp);
        }

        public static long ToUnixTimestamp(this DateTime time)
        {
            if (time.Kind != DateTimeKind.Utc)
            {
                time = time.ToUniversalTime();
            }
            var seed = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
            return (long)time.Subtract(seed).TotalSeconds;
        }

        public static bool IsMimeTypeInline(this string mimeType)
        {
            return mimeType.StartsWith("image", StringComparison.InvariantCultureIgnoreCase) ||
                   mimeType.StartsWith("video", StringComparison.InvariantCultureIgnoreCase);
                   //mimeType.EndsWith("pdf", StringComparison.InvariantCultureIgnoreCase);
        }

        public static _AttachmentPartialModel ToModel(this PhpbbAttachments dbAttachmentRecord)
        {
            return new _AttachmentPartialModel()
            {
                FileName = dbAttachmentRecord.RealFilename,
                Comment = dbAttachmentRecord.AttachComment,
                Id = dbAttachmentRecord.AttachId,
                IsRenderedInline = dbAttachmentRecord.Mimetype.IsMimeTypeInline(),
                MimeType = dbAttachmentRecord.Mimetype,
                DownloadCount = dbAttachmentRecord.DownloadCount,
                FileSize = dbAttachmentRecord.Filesize
            };
        }

        public static T RunSync<T>(this Task<T> asyncTask)
        {
            return asyncTask.GetAwaiter().GetResult();
        }

        public static Bitmap ToImage(this IFormFile file)
        {
            if (file == null)
            {
                return null;
            }
            try
            {
                var stream = file.OpenReadStream();
                using (var bmp = new Bitmap(stream))
                {
                    stream.Seek(0, SeekOrigin.Begin);
                    return bmp;
                }
            }
            catch
            {
                return null;
            }
        }
    }
}
