using LightningQueues;
using LightningQueues.Storage.LMDB;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace PhpbbInDotnet.BackgroundProcessing
{
    class BackgroundProcessingSession : IBackgroundProcessingSession
    {
        private readonly Queue _queue;

        public BackgroundProcessingSession(ILogger<BackgroundProcessingSession> logger) 
        {
            _queue = new QueueConfiguration().StoreWithLmdb("background-queues").LogWith(logger).BuildQueue();
            _queue.Start();
        }

        public async Task SendMessage<TMessage>(TMessage message, CancellationToken cancellationToken)
        {
            using var stream = new MemoryStream();
            await JsonSerializer.SerializeAsync(stream, message, cancellationToken: cancellationToken);
            stream.Seek(0, SeekOrigin.Begin);
            var rawMessage = new Message
            {
                Data = stream.ToArray(),
                Id = MessageId.GenerateRandom(),
                Queue = "add-post"
            };
            _queue.Send(rawMessage);
        }

        public void Dispose()
        {
            _queue.Dispose();
        }
    }
}
