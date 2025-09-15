namespace PhpbbInDotnet.BackgroundProcessing
{
    public interface IBackgroundProcessingSession : IDisposable
    {
        public Task SendMessage<TMessage>(TMessage message, CancellationToken cancellationToken);
    }
}
