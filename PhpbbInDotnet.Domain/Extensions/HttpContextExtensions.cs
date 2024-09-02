using Microsoft.AspNetCore.Http;

namespace PhpbbInDotnet.Domain.Extensions
{
    public static class HttpContextExtensions
    {
        public static string? GetIpAddress(this HttpContext context)
        {
            if (context.Request.Headers.TryGetValue("CF-Connecting-IP", out var val))
            {
                var str = val.ToString();
                if (!string.IsNullOrWhiteSpace(str))
                {
                    return str;
                }
            }

            return context.Connection.RemoteIpAddress?.ToString();
        }
    }
}
