using PhpbbInDotnet.Objects.Messages;
using System.Diagnostics.CodeAnalysis;

namespace PhpbbInDotnet.BackgroundProcessing
{
    internal static class QueueUtility
    {
        private static readonly IReadOnlyDictionary<Type, string> _dict =
            AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(s => s.GetTypes())
                .Where(t => typeof(IBackgroundMessage).IsAssignableFrom(t) && t.IsClass)
                .Select(t => (queueName: (Activator.CreateInstance(t) as IBackgroundMessage)?.QueueName, type: t))
                .Where(x => !string.IsNullOrWhiteSpace(x.queueName))
                .ToDictionary(k => k.type, v => v.queueName!);

        internal static IEnumerable<string> AllQueueNames => _dict.Values;

        internal static bool TryGetQueueName(Type messageType, [MaybeNullWhen(false)] out string? queueName)
        {
            if (_dict.TryGetValue(messageType, out queueName))
            {
                return true;
            }
            return false;
        }
    }
}
