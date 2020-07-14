using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Events;
using System.IO;
using System.Threading.Tasks;

namespace Serverless.Forum
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            await WebHost.CreateDefaultBuilder(args)
                .UseStartup<Startup>()
                .UseSerilog((context, config) =>
                {
                    if (context.HostingEnvironment.IsDevelopment())
                    {
                        config.WriteTo.Console();
                    }
                    else
                    {
                        config.WriteTo.File(
                            path: Path.Combine("logs", "log.txt"),
                            restrictedToMinimumLevel: LogEventLevel.Warning,
                            rollingInterval: RollingInterval.Day
                        );
                    }
                })
                .UseIIS()
                .Build()
                .RunAsync();
        }
    }
}
