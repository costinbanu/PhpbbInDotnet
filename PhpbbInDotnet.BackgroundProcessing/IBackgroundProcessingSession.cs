using PhpbbInDotnet.Objects.Messages;

namespace PhpbbInDotnet.BackgroundProcessing
{
    public interface IBackgroundProcessingSession
    {
        public Task SendMessage<TMessage>(TMessage message, CancellationToken cancellationToken = default) where TMessage : class, IBackgroundMessage, new();
    }
}
