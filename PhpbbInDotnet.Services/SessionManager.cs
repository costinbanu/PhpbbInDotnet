using Microsoft.Extensions.Configuration;
using PhpbbInDotnet.Domain;
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

        //TODO what if it is deployed on multiple instances? Arguably not critical, but still;

        public SessionManager(IConfiguration configuration)
        {
            _sessionCache = new ConcurrentDictionary<string, Item<string, string>>();
            _verifiedBotsCache = new ConcurrentDictionary<string, Item<string, BotData>>();
            _rateLimitCache = new ConcurrentDictionary<string, ConcurrentDictionary<string, Item<string, DateTimeOffset>>>();
            _uniqueVisitorsCache = new ConcurrentDictionary<string, Item<string, int>>();
            _rateLimitOptions = configuration.GetObject<RateLimitOptions>();
        }

        public void UpsertSession(string sessionId, TimeSpan expiration)
        {
            _sessionCache.TryAdd(sessionId, new Item<string, string>(sessionId, string.Empty, expiration, _sessionCache));
        }

        public int GetActiveSessionCount()
            => _sessionCache.Count;

        public void UpsertBot(string ip, string userAgent, string sessionId, TimeSpan expiration)
        {
            var data = new BotData
            {
                EntryTime = DateTime.UtcNow,
                IP = ip,
                UserAgent = userAgent,
                SessionId = sessionId,
            };

            _verifiedBotsCache.AddOrUpdate(
                key: sessionId,
                addValue: new Item<string, BotData>(sessionId, data, expiration, _verifiedBotsCache),
                updateValueFactory: (_, curValue) =>
                {
                    curValue.UpdateValueAndResetTimer(data);
                    return curValue;
                });
        }

        public IEnumerable<BotData> GetBots()
            => _verifiedBotsCache.Select(x => x.Value.Value);

        public int GetTotalActiveBotCount()
            => _verifiedBotsCache.Count;

        public int GetUniqueBotCount()
            => _verifiedBotsCache.Select(x => x.Value.Value).GroupBy(b => b.UserAgent).Count();

        public bool ShouldRateLimit(string userAgent, string ip, string? sessionId, int? userId, UserType userType)
        {
            var key = userType switch
            {
                UserType.VerifiedBot => userAgent,
                UserType.RegisteredUser => $"{userAgent}-{ip}-{userId}",
                _ => $"{userAgent}-{ip}-{sessionId}"
            };
            var threshold = userType switch
            {
                UserType.RegisteredUser => _rateLimitOptions.RequestThresholdForRegisteredUsers,
                _ => _rateLimitOptions.RequestThresholdForOtherUsers
            };

            var now = DateTimeOffset.UtcNow;
            var value = _rateLimitCache.AddOrUpdate(
                key: key,
                addValueFactory: _ =>
                {
                    var dict = new ConcurrentDictionary<string, Item<string, DateTimeOffset>>();
                    var subkey = Guid.NewGuid().ToString();
                    dict.TryAdd(subkey, new Item<string, DateTimeOffset>(subkey, now, _rateLimitOptions.RequestTimeWindow, dict));
                    return dict;
                },
                updateValueFactory: (_, existingDict) =>
                {
                    if (existingDict.Count <= threshold)
                    {
                        var subkey = Guid.NewGuid().ToString();
                        existingDict.TryAdd(subkey, new Item<string, DateTimeOffset>(subkey, now, _rateLimitOptions.RequestTimeWindow, existingDict));
                    }
                    return existingDict;
                });

            return value.Count > threshold;
        }

        public bool IsSessionStarted(string? sessionId)
            => !string.IsNullOrWhiteSpace(sessionId) && (_sessionCache.ContainsKey(sessionId) || _verifiedBotsCache.ContainsKey(sessionId));

        private readonly ConcurrentDictionary<string, Item<string, string>> _sessionCache;
        private readonly ConcurrentDictionary<string, Item<string, BotData>> _verifiedBotsCache;
        private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, Item<string, DateTimeOffset>>> _rateLimitCache;
        private readonly ConcurrentDictionary<string, Item<string, int>> _uniqueVisitorsCache;
        private readonly RateLimitOptions _rateLimitOptions;

        private class Item<TKey, TValue> : IDisposable where TKey : notnull
        {
            private readonly Timer _timer;

            internal TValue Value { get; private set; }

            internal void ResetTimer()
            {
                _timer.Stop();
                _timer.Start();
            }

            internal Item(TKey key, TValue value, TimeSpan expiration, ConcurrentDictionary<TKey, Item<TKey, TValue>> cache)
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

            internal void UpdateValueAndResetTimer(TValue newValue)
            {
                Value = newValue;
                ResetTimer();
            }

            public void Dispose()
            {
                _timer?.Stop();
                _timer?.Dispose();
            }
        }
    }
}
