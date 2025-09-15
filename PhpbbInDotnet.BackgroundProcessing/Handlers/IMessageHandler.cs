namespace PhpbbInDotnet.BackgroundProcessing.Handlers
{
    internal interface IMessageHandler<TMessage>
    {
        Task Handle(TMessage message, CancellationToken cancellationToken);
    }
}
