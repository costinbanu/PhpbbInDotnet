using PhpbbInDotnet.Objects;
using System;
using System.Collections.Generic;

namespace PhpbbInDotnet.Services
{
    public interface IAnonymousSessionCounter
    {
        int GetTotalActiveBotCount();
        int GetActiveSessionCount();
        int GetUniqueBotCount();
        IEnumerable<BotData> GetBots();
        void UpsertBot(string ip, string userAgent, TimeSpan expiration);
        void UpsertSession(string sessionId, TimeSpan expiration);
        bool ShouldRateLimit(string userAgent, string ip, string? sessionId, int threshold, TimeSpan timeWindow);
    }
}