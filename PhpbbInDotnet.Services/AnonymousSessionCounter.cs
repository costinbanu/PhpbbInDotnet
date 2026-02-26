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
            _verifiedBotsCache = new ConcurrentDictionary<string, Item<ConcurrentBag<BotData>>>();
            _rateLimitCache = new ConcurrentDictionary<string, ConcurrentDictionary<string, Item<DateTimeOffset>>>();
            _ipsByUserAgentCache = new ConcurrentDictionary<string, Item<ConcurrentDictionary<string, DateTimeOffset>>>();
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

            _verifiedBotsCache.AddOrUpdate(
                key: userAgent,
                addValue: new Item<ConcurrentBag<BotData>>(userAgent, new ConcurrentBag<BotData> { data }, expiration, _verifiedBotsCache),
                updateValueFactory: (_, curValue) =>
                {
                    curValue.Value.Add(data);
                    curValue.ResetTimer();
                    return curValue;
                });
        }

        public IEnumerable<BotData> GetBots()
            => _verifiedBotsCache.SelectMany(x => x.Value.Value);

        public int GetTotalActiveBotCount()
            => _verifiedBotsCache.SelectMany(x => x.Value.Value).Count();

        public int GetUniqueBotCount()
            => _verifiedBotsCache.Count;

        public bool ShouldRateLimit(string userAgent, string ip, string? sessionId, int threshold, TimeSpan timeWindow)
            => ShouldRateLimitByInstance(userAgent, ip, sessionId, threshold, timeWindow)
                || ShouldRateLimitByVolume(userAgent, ip, sessionId, threshold, timeWindow);

        //no more than threshold requests for the same user agent + IP + sessionId in the time window
        private bool ShouldRateLimitByVolume(string userAgent, string ip, string? sessionId, int threshold, TimeSpan timeWindow)
        {
            var key = $"{userAgent}-{ip}-{sessionId}";
            var now = DateTimeOffset.UtcNow;

            var value = _rateLimitCache.AddOrUpdate(
                key: key,
                addValueFactory: _ =>
                {
                    var dict = new ConcurrentDictionary<string, Item<DateTimeOffset>>();
                    var subkey = Guid.NewGuid().ToString();
                    dict.TryAdd(subkey, new Item<DateTimeOffset>(subkey, now, timeWindow, dict));
                    return dict;
                },
                updateValueFactory: (_, existingDict) =>
                {
                    if (existingDict.Count < threshold)
                    {
                        var subkey = Guid.NewGuid().ToString();
                        existingDict.TryAdd(subkey, new Item<DateTimeOffset>(subkey, now, timeWindow, existingDict));
                    }
                    return existingDict;
                });

            return value.Count >= threshold;
        }

        //no more than threshold different IPs for the same user agent + sessionId in the time window
        private bool ShouldRateLimitByInstance(string userAgent, string ip, string? sessionId, int threshold, TimeSpan timeWindow)
        {
            var key = $"{userAgent}-{sessionId}";
            var now = DateTimeOffset.UtcNow;
            var value = _ipsByUserAgentCache.AddOrUpdate(
                key: key,
                addValue: new Item<ConcurrentDictionary<string, DateTimeOffset>>(key, new ConcurrentDictionary<string, DateTimeOffset> { [ip] = now }, timeWindow, _ipsByUserAgentCache),
                updateValueFactory: (_, curValue) =>
                {
                    if (curValue.Value.Count < threshold && curValue.Value.TryAdd(ip, now))
                    {
                        curValue.ResetTimer();
                    }
                    return curValue;
                });

            return value.Value.Count >= threshold;
        }

        private readonly ConcurrentDictionary<string, Item<string>> _sessionCache;
        private readonly ConcurrentDictionary<string, Item<ConcurrentBag<BotData>>> _verifiedBotsCache;
        private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, Item<DateTimeOffset>>> _rateLimitCache;
        private readonly ConcurrentDictionary<string, Item<ConcurrentDictionary<string, DateTimeOffset>>> _ipsByUserAgentCache;

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
