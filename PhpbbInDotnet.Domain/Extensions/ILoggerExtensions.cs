using Serilog;
using System;

namespace PhpbbInDotnet.Domain.Extensions
{
    public static class ILoggerExtensions
    {
        public static string ErrorWithId(this ILogger logger, Exception exception, string? format = null, params object[] @params)
        {
            var id = Guid.NewGuid().ToString("n");
            if (string.IsNullOrWhiteSpace(format))
            {
                logger.Error(exception, "Exception id: {id}.", id);
            }
            else
            {
                logger.Error(exception, $"Exception id: {id}. Message: {format}", @params);
            }
            return id;
        }

        public static string WarningWithId(this ILogger logger, Exception exception, string? format = null, params object[] @params)
        {
            var id = Guid.NewGuid().ToString("n");
            if (string.IsNullOrWhiteSpace(format))
            {
                logger.Warning(exception, "Exception id: {id}.", id);
            }
            else
            {
                logger.Warning(exception, $"Exception id: {id}. Message: {format}", @params);
            }
            return id;
        }

        public static void Error(this ILogger logger, Exception exception)
        {
            logger.Error(exception, "An unexpected error occured.");
        }

        public static void Warning(this ILogger logger, Exception exception)
        {
            logger.Warning(exception, "An unexpected error occured.");
        }
    }
}
