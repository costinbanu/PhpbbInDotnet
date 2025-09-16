using LightningQueues;
using PhpbbInDotnet.Objects.Messages;
using System.Text.Json;

namespace PhpbbInDotnet.BackgroundProcessing
{
    class BackgroundProcessingSession(Queue queue) : IBackgroundProcessingSession
    {
        public async Task SendMessage<TMessage>(TMessage message, CancellationToken cancellationToken = default) where TMessage : class, IBackgroundMessage, new()
        {
            using var stream = new MemoryStream();
            await JsonSerializer.SerializeAsync(stream, message, cancellationToken: cancellationToken);
            stream.Seek(0, SeekOrigin.Begin);
            var rawMessage = new Message
            {
                Data = stream.ToArray(),
                Id = MessageId.GenerateRandom(),
                Queue = message.QueueName,
                Destination = new Uri($"lq.tcp://{queue.Endpoint}")
            };
            queue.Send(rawMessage);
        }
    }
}
