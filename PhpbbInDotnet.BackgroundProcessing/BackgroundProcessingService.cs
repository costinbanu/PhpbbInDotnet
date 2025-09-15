using LightningQueues;
using LightningQueues.Storage.LMDB;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PhpbbInDotnet.BackgroundProcessing.Handlers;
using PhpbbInDotnet.Objects.Messages;
using System.Text.Json;
using System.Threading;

namespace PhpbbInDotnet.BackgroundProcessing
{
    public class BackgroundProcessingService : IHostedService, IDisposable
    {
        private readonly Queue _queue;
        private readonly IServiceProvider _serviceProvider;

        public BackgroundProcessingService(IServiceProvider serviceProvider, ILogger<BackgroundProcessingService> logger)
        {
            _queue = new QueueConfiguration().StoreWithLmdb("background-queues").LogWith(logger).BuildQueue();
            _queue.Start();
            _serviceProvider = serviceProvider;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            //hacky hack https://github.com/dotnet/runtime/issues/36063
            await Task.Yield();



            await foreach (var message in _queue.Receive("add-post", cancellationToken).WithCancellation(cancellationToken))
            {
                using var stream = new MemoryStream(message.Message.Data);
                var messageBody = await JsonSerializer.DeserializeAsync<AddPostCommand>(stream, cancellationToken: cancellationToken);

                if (messageBody != null)
                {
                    using var scope = _serviceProvider.CreateScope();
                    var handler = scope.ServiceProvider.GetRequiredService<IMessageHandler<AddPostCommand>>();
                    await handler.Handle(messageBody, cancellationToken);

                    message.QueueContext.SuccessfullyReceived();
                }
            }
        }

        public Task StopAsync(CancellationToken cancellationToken)
            => Task.CompletedTask;

        public void Dispose()
        {
            _queue.Dispose();
        }
    }
}
