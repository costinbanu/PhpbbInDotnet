using LightningQueues;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PhpbbInDotnet.BackgroundProcessing.Handlers;
using PhpbbInDotnet.Objects.Messages;
using System.Text.Json;

namespace PhpbbInDotnet.BackgroundProcessing
{
    public class BackgroundProcessingService(Queue queue, IServiceProvider serviceProvider) : IHostedService, IDisposable
    {
        private Task? _allHandlersRunningTask;
        private CancellationTokenSource? _cancellationTokenSource;

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _cancellationTokenSource = new CancellationTokenSource();

            cancellationToken.ThrowIfCancellationRequested();

            _allHandlersRunningTask = Task.WhenAll(
                Handle<AddPostCommand>(_cancellationTokenSource.Token));
            
            return Task.CompletedTask;
        }

        private async Task Handle<TMessage>(CancellationToken cancellationToken) where TMessage : class, IBackgroundMessage, new()
        {
            if (!QueueUtility.TryGetQueueName(typeof(TMessage), out var queueName))
            {
                return;
            }

            await foreach (var message in queue.Receive(queueName, cancellationToken).WithCancellation(cancellationToken))
            {
                using var stream = new MemoryStream(message.Message.Data);
                var messageBody = await JsonSerializer.DeserializeAsync<TMessage>(stream, cancellationToken: cancellationToken);

                if (messageBody != null)
                {
                    using var scope = serviceProvider.CreateScope();
                    var handler = scope.ServiceProvider.GetRequiredService<IMessageHandler<TMessage>>();
                    await handler.Handle(messageBody, cancellationToken);

                    message.QueueContext.SuccessfullyReceived();
                    message.QueueContext.CommitChanges();
                }
            }
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            if (_cancellationTokenSource is not null)
            {
                await _cancellationTokenSource.CancelAsync();
            }
        }

        public void Dispose()
        {
            _cancellationTokenSource?.Dispose();
            _allHandlersRunningTask?.Dispose();
        }
    }
}
