using Dapper;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Serverless.Forum.Contracts;
using Serverless.Forum.ForumDb;
using Serverless.Forum.Pages.CustomPartials;
using System;
using System.Linq;
using System.Security.Claims;
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

        public static async Task<LoggedUser> ToLoggedUserAsync(this ClaimsPrincipal principal, Utils utils)
        {
            return await utils.DecompressObjectAsync<LoggedUser>(Convert.FromBase64String(principal.Claims.FirstOrDefault()?.Value ?? string.Empty));
        }

        public static async Task<ClaimsPrincipal> ToClaimsPrincipalAsync(this PhpbbUsers user, ForumDbContext dbContext, Utils utils)
        {
            using (var connection = dbContext.Database.GetDbConnection())
            {
                await connection.OpenAsync();
                DefaultTypeMap.MatchNamesWithUnderscores = true;

                using (var multi = await connection.QueryMultipleAsync("CALL `forum`.`get_user_details`(@UserId);", new { user.UserId }))
                {
                    var intermediary = new LoggedUser
                    {
                        UserId = user.UserId,
                        Username = user.Username,
                        UsernameClean = user.UsernameClean,
                        UserPermissions = await multi.ReadAsync<LoggedUser.Permissions>(),
                        Groups = (await multi.ReadAsync<uint>()).Select(x => checked((int)x)),
                        TopicPostsPerPage = (await multi.ReadAsync()).ToDictionary(key => checked((int)key.topic_id), value => checked((int)value.post_no)),
                        UserDateFormat = user.UserDateformat
                    };

                    var identity = new ClaimsIdentity(CookieAuthenticationDefaults.AuthenticationScheme);
                    identity.AddClaim(new Claim(ClaimTypes.UserData, Convert.ToBase64String(await utils.CompressObjectAsync(intermediary))));
                    return new ClaimsPrincipal(identity);
                }
            }
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
                MimeType = dbAttachmentRecord.Mimetype,
                DownloadCount = dbAttachmentRecord.DownloadCount
            };
        }

        public static T RunSync<T>(this Task<T> asyncTask)
        {
            return asyncTask.GetAwaiter().GetResult();
        }
    }
}
