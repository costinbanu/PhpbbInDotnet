using System;
using System.Collections.Concurrent;
using System.Timers;

namespace PhpbbInDotnet.Forum.Utilities
{
    public class AnonymousSessionCounter
    {
        public AnonymousSessionCounter()
        {
            _cache = new ConcurrentDictionary<string, Item>();
        }

        public void UpsertSession(string sessionId, TimeSpan expiration)
        {
            _cache.TryAdd(sessionId, new Item(sessionId, expiration, _cache));
        }

        public int GetActiveSessionCount()
            => _cache.Count;

        private readonly ConcurrentDictionary<string, Item> _cache;

        private class Item : IDisposable
        {
            private readonly Timer _timer;
            private readonly string _value;

            internal Item(string value, TimeSpan expiration, ConcurrentDictionary<string, Item> cache)
            {
                _value = value;
                _timer = new Timer(expiration.TotalMilliseconds);
                _timer.Elapsed += new ElapsedEventHandler((_, __) =>
                {
                    if (cache.TryRemove(_value, out var obj))
                    {
                        obj?.Dispose();
                    }
                });
                _timer.Start();
            }

            public void Dispose()
            {
                _timer?.Stop();
                _timer?.Dispose();
            }
        }
    }
}
