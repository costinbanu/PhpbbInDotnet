using Microsoft.Extensions.Configuration;

namespace PhpbbInDotnet.Domain.Extensions
{
    public static class IConfigurationExtensions
    {
        public static T GetObject<T>(this IConfiguration config, string? sectionName = null) where T : notnull
            => config.GetSection(sectionName ?? typeof(T).Name).Get<T>()!;

        public static T? GetObjectOrDefault<T>(this IConfiguration config, string? sectionName = null)
            => config.GetSection(sectionName ?? typeof(T).Name).Get<T>();
    }
}
