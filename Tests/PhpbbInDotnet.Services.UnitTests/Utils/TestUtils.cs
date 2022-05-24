using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Text;

namespace PhpbbInDotnet.Services.UnitTests.Utils
{
    internal static class TestUtils
    {
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
    }
}
