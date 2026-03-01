using System;
using System.Linq;
using DeviceDetectorNET;
using Microsoft.AspNetCore.Http;
using Serilog;

namespace PhpbbInDotnet.Domain.Extensions
{
    public static class HttpContextExtensions
    {
        const string isBotKey = "RequestIsMadeByBot";

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

        public static bool IsBot(this HttpContext context)
        {
            if (context.Items.TryGetValue(isBotKey, out var isBotObj) && isBotObj is bool isBot)
            {
                return isBot;
            }

            var userAgent = context.Request.Headers.UserAgent.ToString();
            try
            {
                var dd = new DeviceDetector(userAgent, ClientHints.Factory(context.Request.Headers.ToDictionary(a => a.Key, a => a.Value.ToArray().FirstOrDefault())));
                dd.Parse();
                isBot = dd.IsBot();
                context.Items[isBotKey] = isBot;
                return isBot;
            }
            catch (Exception ex)
            {
                var loggerService = context.RequestServices.GetService(typeof(ILogger));
                if (loggerService is ILogger logger)
                {
                    logger.Warning(ex, "Failed to detect if session is bot. User agent: {userAgent}, IP: {ip}", userAgent, context.GetIpAddress());
                }
            }

            return false;
        }
    }
}
