using Newtonsoft.Json;
using Serverless.Forum.Contracts;
using Serverless.Forum.forum;
using Serverless.Forum.Pages;
using System;
using System.Linq;
using System.Security.Claims;

namespace Serverless.Forum.Utilities
{
    public static class Extensions
    {
        public static DateTime TimestampToLocalTime(this long timestamp)
        {
            var seed = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
            return seed.AddSeconds(timestamp);
        }

        public static long LocalTimeToTimestamp(this DateTime time)
        {
            var seed = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
            return (int)time.Subtract(seed).TotalSeconds;
        }

        public static LoggedUser ToLoggedUser(this ClaimsPrincipal principal)
        {
            return JsonConvert.DeserializeObject<LoggedUser>(principal.Claims.FirstOrDefault()?.Value ?? "{}");
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
                Id = dbAttachmentRecord.AttachId,
                IsRenderedInline = dbAttachmentRecord.Mimetype.IsMimeTypeInline(),
                MimeType = dbAttachmentRecord.Mimetype
            };
        }
    }
}
