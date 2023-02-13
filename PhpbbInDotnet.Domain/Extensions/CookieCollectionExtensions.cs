using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using PhpbbInDotnet.Domain.Utilities;
using System;
using System.Diagnostics.CodeAnalysis;

namespace PhpbbInDotnet.Domain.Extensions
{
    public static class CookieCollectionExtensions
    {
        public static void AddObject<T>(this IResponseCookies cookies, string key, T value, TimeSpan maxAge)
            => cookies.Append(
                key: key,
                value: JsonConvert.SerializeObject(value),
                options: new CookieOptions
                {
                    Secure = true,
                    SameSite = SameSiteMode.Strict,
                    MaxAge = maxAge,
                });

        public static bool TryGetObject<T>(this IRequestCookieCollection cookies, string key, [MaybeNullWhen(false)] out T result)
        {
            if (cookies.TryGetValue(key, out var serialized) && serialized is not null)
            {
                try
                {
                    result = JsonConvert.DeserializeObject<T>(serialized);
                    return result is not null;
                }
                catch { }
            }
            result = default;
            return false;
        }

        public static void SaveForumLogin(this IResponseCookies cookies, int userId, int forumId)
            => cookies.AddObject(GetForumLoginKey(forumId), HashUtility.ComputeCrc64Hash(userId + forumId), TimeSpan.FromMinutes(30));

        public static bool IsUserLoggedIntoForum(this IRequestCookieCollection cookies, int userId, int forumId)
            => cookies.TryGetObject<long>(GetForumLoginKey(forumId), out var hash) && hash == HashUtility.ComputeCrc64Hash(userId + forumId);

        static string GetForumLoginKey(int forumId)
            => $"ForumLogin_{forumId}";
    }
}
