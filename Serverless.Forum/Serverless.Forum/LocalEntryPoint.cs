using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;

namespace Serverless.Forum
{
    public class Program
    {
        public static void Main(string[] args)
        {
            BuildWebHost(args).Run();
        }

        public static IWebHost BuildWebHost(string[] args) =>
            WebHost.CreateDefaultBuilder(args)
                .UseStartup<Startup>()
                .UseKestrel(options =>
                {
                    options.Limits.MaxRequestHeadersTotalSize = 1048576;
                })
                .Build();
    }
}
