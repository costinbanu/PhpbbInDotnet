using PhpbbInDotnet.Objects;
using System;
using System.Collections.Generic;

namespace PhpbbInDotnet.Services
{
    public interface IAnonymousSessionCounter
    {
        int GetTotalActiveBotCount();
        int GetActiveBotCountByUserAgent(string userAgent);
        DateTime? GetLastVisit(string userAgent);
        int GetActiveSessionCount();
        int GetUniqueBotCount();
        IEnumerable<BotData> GetBots();
        void UpsertBot(string ip, string userAgent, TimeSpan expiration);
        void UpsertSession(string sessionId, TimeSpan expiration);
    }
}