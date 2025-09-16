using LightningDB;
using LightningQueues;
using LightningQueues.Storage.LMDB;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PhpbbInDotnet.BackgroundProcessing.Handlers;
using PhpbbInDotnet.Domain;
using PhpbbInDotnet.Objects.Messages;

namespace PhpbbInDotnet.BackgroundProcessing
{
    public static class ServiceCollectionExtensions
    {
        private static readonly EnvironmentConfiguration _env = new()
        {
            MapSize = Constants.ONE_MB * 100,
            MaxDatabases = 2,
            MaxReaders = 1,
        };

        public static IServiceCollection AddBackgroundProcessing(this IServiceCollection services)
        {
            services.AddSingleton(sp =>
            {
                var factory = sp.GetRequiredService<ILoggerFactory>();
                var logger = factory.CreateLogger(nameof(BackgroundProcessing));
                var queue = new QueueConfiguration().WithDefaults().StoreWithLmdb("background-queues", _env).LogWith(logger).BuildQueue();
                queue.Start();
                foreach (var queueName in QueueUtility.AllQueueNames)
                {
                    queue.CreateQueue(queueName);
                }
                return queue;
            });

            services.AddScoped<IMessageHandler<AddPostCommand>, AddPostCommandHandler>();

            services.AddScoped<IBackgroundProcessingSession, BackgroundProcessingSession>();
            
            services.AddSingleton<BackgroundProcessingService>();
            services.AddHostedService<BackgroundProcessingService>();

            return services;
        }
    }
}
