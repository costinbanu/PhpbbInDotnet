using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Timers;

namespace PhpbbInDotnet.Utilities
{
    public class AnonymousSessionCounter
    {
        public AnonymousSessionCounter()
        {
            _sessionCache = new ConcurrentDictionary<string, Item>();
            _ipCache = new ConcurrentDictionary<string, Item>();
        }

        public void UpsertSession(string sessionId, TimeSpan expiration)
        {
            _sessionCache.TryAdd(sessionId, new Item(sessionId, expiration, _sessionCache));
        }

        public int GetActiveSessionCount()
            => _sessionCache.Count;

        public void UpsertBot(string ip, string userAgent, TimeSpan expiration)
        {
            _ipCache.TryAdd(
                ip, 
                new Item(
                    JsonConvert.SerializeObject(new BotData
                    {
                        IP = ip,
                        UserAgent = userAgent,
                        EntryTime = DateTime.UtcNow
                    }), 
                    expiration, 
                    _ipCache
                )
            );
        }

        public IEnumerable<BotData> GetBots()
            => _ipCache.Select(x => JsonConvert.DeserializeObject<BotData>(x.Value.Value));

        public int GetActiveBotCount()
            => _ipCache.Count;

        private readonly ConcurrentDictionary<string, Item> _sessionCache;
        private readonly ConcurrentDictionary<string, Item> _ipCache;

        private class Item : IDisposable
        {
            private readonly Timer _timer;

            internal string Value { get; }

            internal Item(string value, TimeSpan expiration, ConcurrentDictionary<string, Item> cache)
            {
                Value = value;
                _timer = new Timer(expiration.TotalMilliseconds);
                _timer.Elapsed += new ElapsedEventHandler((_, __) =>
                {
                    if (cache.TryRemove(Value, out var obj))
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

        public class BotData
        {
            public string IP { get; set; }

            public string UserAgent { get; set; }

            public DateTime EntryTime { get; set; }
        }
    }
}
