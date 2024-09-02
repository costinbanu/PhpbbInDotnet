using PhpbbInDotnet.Objects;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Timers;

namespace PhpbbInDotnet.Services
{
    class AnonymousSessionCounter : IAnonymousSessionCounter
    {
        public AnonymousSessionCounter()
        {
            _sessionCache = new ConcurrentDictionary<string, Item<string>>();
            _userAgentCache = new ConcurrentDictionary<string, Item<ConcurrentBag<BotData>>>();
        }

        public void UpsertSession(string sessionId, TimeSpan expiration)
        {
            _sessionCache.TryAdd(sessionId, new Item<string>(sessionId, string.Empty, expiration, _sessionCache));
        }

        public int GetActiveSessionCount()
            => _sessionCache.Count;

        public void UpsertBot(string ip, string userAgent, TimeSpan expiration)
        {
            var data = new BotData
            {
                EntryTime = DateTime.UtcNow,
                IP = ip,
                UserAgent = userAgent
            };

            _userAgentCache.AddOrUpdate(
                key: userAgent,
                addValue: new Item<ConcurrentBag<BotData>>(userAgent, new ConcurrentBag<BotData> { data }, expiration, _userAgentCache),
                updateValueFactory: (_, curValue) =>
                {
                    curValue.Value.Add(data);
                    curValue.ResetTimer();
                    return curValue;
                });
        }

        public IEnumerable<BotData> GetBots()
            => _userAgentCache.SelectMany(x => x.Value.Value);

        public int GetTotalActiveBotCount()
            => _userAgentCache.SelectMany(x => x.Value.Value).Count();

        public int GetUniqueBotCount()
            => _userAgentCache.Count;

        public int GetActiveBotCountByUserAgent(string userAgent)
            => _userAgentCache.TryGetValue(userAgent, out var item) ? item.Value.Count : 0;

        public DateTime? GetLastVisit(string userAgent)
            => _userAgentCache.TryGetValue(userAgent, out var item) ? item.Value.Max(b => b.EntryTime) : null;

        private readonly ConcurrentDictionary<string, Item<string>> _sessionCache;
        private readonly ConcurrentDictionary<string, Item<ConcurrentBag<BotData>>> _userAgentCache;

        private class Item<TValue> : IDisposable
        {
            private readonly Timer _timer;

            internal TValue Value { get; private set; }

            internal void ResetTimer()
            {
                _timer.Stop();
                _timer.Start();
            }

            internal Item(string key, TValue value, TimeSpan expiration, ConcurrentDictionary<string, Item<TValue>> cache)
            {
                Value = value;
                _timer = new Timer(expiration.TotalMilliseconds)
                {
                    AutoReset = false
                };
                _timer.Elapsed += new ElapsedEventHandler((_, __) =>
                {
                    if (cache.TryRemove(key, out var obj))
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
