using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Events;
using System.IO;
using System.Threading.Tasks;

namespace PhpbbInDotnet.Forum
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var format = "[{Timestamp:yyyy-MM-dd HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}{NewLine}";
            await WebHost.CreateDefaultBuilder(args)
                .UseStartup<Startup>()
                .UseSerilog((context, config) =>
                {
                    if (context.HostingEnvironment.IsDevelopment())
                    {
                        config.WriteTo.Console(outputTemplate: format);
                    }
                    else
                    {
                        config.WriteTo.File(
                            path: Path.Combine("logs", "log.txt"),
                            restrictedToMinimumLevel: LogEventLevel.Warning,
                            rollingInterval: RollingInterval.Day,
                            outputTemplate: format
                        );
                    }
                })
                .UseIIS()
                .Build()
                .RunAsync();
        }
    }
}
