﻿using Microsoft.AspNetCore.Mvc;
using Serverless.Forum.Contracts;
using Serverless.Forum.ForumDb;
using Serverless.Forum.Pages;
using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace Serverless.Forum.Utilities
{
    public static class Extensions
    {
        public static DateTime TimestampToUtcTime(this long timestamp)
        {
            var seed = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
            return seed.AddSeconds(timestamp);
        }

        public static long UtcTimeToTimestamp(this DateTime time)
        {
            if (time.Kind != DateTimeKind.Utc)
            {
                time = time.ToUniversalTime();
            }
            var seed = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
            return (long)time.Subtract(seed).TotalSeconds;
        }

        public static async Task<LoggedUser> ToLoggedUser(this ClaimsPrincipal principal, Utils utils)
        {
            return await utils.DecompressObjectAsync<LoggedUser>(principal.Claims.FirstOrDefault()?.Value);
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

        //public static T RunSync<T>(this Task<T> task)
        //{
        //    Task.WaitAll(task);
        //    return task.Result;
        //}

        //public static void RunSync(this Task task)
        //{
        //    Task.WaitAll(task);
        //}
    }
}
