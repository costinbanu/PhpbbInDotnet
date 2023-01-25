using Microsoft.AspNetCore.Http;
using PhpbbInDotnet.Domain.Utilities;
using System;

namespace PhpbbInDotnet.Domain.Extensions
{
    public static class CookieCollectionExtensions
    {
        public static void SaveForumLogin(this IResponseCookies cookies, int userId, int forumId)
            => cookies.Append(
                key: GetKey(forumId),
                value: HashUtility.ComputeCrc64Hash(userId).ToString(),
                options: new CookieOptions
                {
                    Secure = true,
                    SameSite = SameSiteMode.Strict,
                    Expires = DateTimeOffset.UtcNow.AddMinutes(30)
                });

        public static bool IsUserLoggedIntoForum(this IRequestCookieCollection cookies, int userId, int forumId)
            => cookies.TryGetValue(GetKey(forumId), out var raw) &&
                long.TryParse(raw, out var hash) &&
                hash == HashUtility.ComputeCrc64Hash(userId);

        static string GetKey(int forumId)
            => $"ForumLogin_{forumId}";
    }
}
