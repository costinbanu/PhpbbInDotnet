using Microsoft.Extensions.DependencyInjection;

namespace PhpbbInDotnet.BackgroundProcessing
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddBackgroundProcessing(this IServiceCollection services)
        {
            //services.AddScoped(sp =>
            //{
            //    var factory = sp.GetRequiredService<ILoggerFactory>();
            //    var logger = factory.CreateLogger(nameof(BackgroundProcessingService));
            //    var queue = new QueueConfiguration().StoreWithLmdb("background-queues").LogWith(logger).BuildQueue();
            //    queue.Start();
            //    return queue;
            //});

            services.AddScoped<IBackgroundProcessingSession, BackgroundProcessingSession>();
            services.AddScoped<BackgroundProcessingService>();
            services.AddHostedService<BackgroundProcessingService>();

            return services;
        }
    }
}
