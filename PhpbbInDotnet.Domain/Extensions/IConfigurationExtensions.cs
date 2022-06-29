using Microsoft.Extensions.Configuration;

namespace PhpbbInDotnet.Domain.Extensions
{
    public static class IConfigurationExtensions
    {
        public static T GetObject<T>(this IConfiguration config, string? sectionName = null)
            => config.GetSection(sectionName ?? typeof(T).Name).Get<T>();
    }
}
