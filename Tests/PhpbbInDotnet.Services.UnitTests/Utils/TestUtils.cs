using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace PhpbbInDotnet.Services.UnitTests.Utils
{
    internal static class TestUtils
    {
        static readonly Regex MultipleWhiteSpace = new(@"\s+", RegexOptions.Compiled | RegexOptions.IgnoreCase, TimeSpan.FromSeconds(10));

        internal static IConfiguration GetAppConfiguration(Action<AppSettingsObject>? setup = null)
        {
            var obj = new ConfigurationBuilder().AddJsonFile("appsettings.json").Build().Get<AppSettingsObject>();
            setup?.Invoke(obj);

            using var stream = new MemoryStream();
            using var writer = new StreamWriter(stream, Encoding.UTF8);
            writer.Write(JsonConvert.SerializeObject(obj));
            writer.Flush();
            stream.Seek(0, SeekOrigin.Begin);

            return new ConfigurationBuilder().AddJsonStream(stream).Build();
        }

        internal static bool MatchesSqlStatement(string originalSql, string match)
            => MultipleWhiteSpace.Replace(originalSql, " ").Equals(MultipleWhiteSpace.Replace(match, " "), StringComparison.InvariantCultureIgnoreCase);
    
        internal static bool MatchesSqlParameters(object originalParams, object match)
            => JsonConvert.SerializeObject(originalParams).Equals(JsonConvert.SerializeObject(match), StringComparison.InvariantCulture);
    }
}
