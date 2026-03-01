using Microsoft.Extensions.Configuration;
using PhpbbInDotnet.Domain.Extensions;
using PhpbbInDotnet.Objects;
using PhpbbInDotnet.Objects.Configuration;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Timers;

namespace PhpbbInDotnet.Services
{
    class SessionManager : ISessionManager
    {
        public SessionManager(IConfiguration configuration)
        {
            _sessionCache = new ConcurrentDictionary<string, Item<string>>();
            _verifiedBotsCache = new ConcurrentDictionary<string, Item<ConcurrentBag<BotData>>>();
            _rateLimitCache = new ConcurrentDictionary<string, ConcurrentDictionary<string, Item<DateTimeOffset>>>();
            _ipsByUserAgentCache = new ConcurrentDictionary<string, Item<ConcurrentDictionary<string, DateTimeOffset>>>();
            _rateLimitOptions = configuration.GetObject<RateLimitOptions>();
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

        public bool ShouldRateLimit(string userAgent, string ip, string? sessionId)
            => ShouldRateLimitByInstance(userAgent, ip, sessionId)
                || ShouldRateLimitByVolume(userAgent, ip, sessionId);

        //no more than requestThreshold requests for the same user agent + IP + sessionId in the requestTimeWindow
        private bool ShouldRateLimitByVolume(string userAgent, string ip, string? sessionId)
        {
            var key = $"{userAgent}-{ip}-{sessionId}";
            var now = DateTimeOffset.UtcNow;

            var value = _rateLimitCache.AddOrUpdate(
                key: key,
                addValueFactory: _ =>
                {
                    var dict = new ConcurrentDictionary<string, Item<DateTimeOffset>>();
                    var subkey = Guid.NewGuid().ToString();
                    dict.TryAdd(subkey, new Item<DateTimeOffset>(subkey, now, _rateLimitOptions.RequestTimeWindow, dict));
                    return dict;
                },
                updateValueFactory: (_, existingDict) =>
                {
                    if (existingDict.Count < _rateLimitOptions.RequestThreshold)
                    {
                        var subkey = Guid.NewGuid().ToString();
                        existingDict.TryAdd(subkey, new Item<DateTimeOffset>(subkey, now, _rateLimitOptions.RequestTimeWindow, existingDict));
                    }
                    return existingDict;
                });

            return value.Count >= _rateLimitOptions.RequestThreshold;
        }

        //no more than clientThreshold different IPs for the same user agent + sessionId in the clientTimeWindow
        private bool ShouldRateLimitByInstance(string userAgent, string ip, string? sessionId)
        {
            var key = $"{userAgent}-{sessionId}";
            var now = DateTimeOffset.UtcNow;
            var value = _ipsByUserAgentCache.AddOrUpdate(
                key: key,
                addValue: new Item<ConcurrentDictionary<string, DateTimeOffset>>(
                    key: key, 
                    value: new ConcurrentDictionary<string, DateTimeOffset> { [ip] = now }, 
                    expiration: _rateLimitOptions.ClientTimeWindow,
                    cache: _ipsByUserAgentCache),
                updateValueFactory: (_, curValue) =>
                {
                    if (curValue.Value.Count < _rateLimitOptions.ClientThreshold && curValue.Value.TryAdd(ip, now))
                    {
                        curValue.ResetTimer();
                    }
                    return curValue;
                });

            return value.Value.Count >= _rateLimitOptions.ClientThreshold;
        }

        private readonly ConcurrentDictionary<string, Item<string>> _sessionCache;
        private readonly ConcurrentDictionary<string, Item<ConcurrentBag<BotData>>> _verifiedBotsCache;
        private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, Item<DateTimeOffset>>> _rateLimitCache;
        private readonly ConcurrentDictionary<string, Item<ConcurrentDictionary<string, DateTimeOffset>>> _ipsByUserAgentCache;
        private readonly RateLimitOptions _rateLimitOptions;

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
